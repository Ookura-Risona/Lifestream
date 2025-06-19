﻿using AutoRetainerAPI;
using Dalamud.Game.Gui.Dtr;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.ChatMethods;
using ECommons.Configuration;
using ECommons.Events;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using ECommons.SimpleGui;
using ECommons.Singletons;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lifestream.Data;
using Lifestream.Enums;
using Lifestream.Game;
using Lifestream.GUI;
using Lifestream.GUI.Windows;
using Lifestream.IPC;
using Lifestream.Movement;
using Lifestream.Schedulers;
using Lifestream.Services;
using Lifestream.Systems;
using Lifestream.Systems.Custom;
using Lifestream.Systems.Legacy;
using Lifestream.Systems.Residential;
using Lifestream.Tasks;
using Lifestream.Tasks.CrossDC;
using Lifestream.Tasks.CrossWorld;
using Lifestream.Tasks.SameWorld;
using Lifestream.Tasks.Shortcuts;
using Lumina.Excel.Sheets;
using NotificationMasterAPI;
using GrandCompany = ECommons.ExcelServices.GrandCompany;

namespace Lifestream;

public unsafe class Lifestream : IDalamudPlugin
{
    public string Name => "Lifestream";
    internal static Lifestream P;
    internal Config Config;
    internal DataStore DataStore;
    internal Memory Memory;
    internal Overlay Overlay;

    internal TinyAetheryte? ActiveAetheryte = null;
    internal AutoRetainerApi AutoRetainerApi;
    internal uint Territory => TerritoryWatcher.GetRealTerritoryType();
    internal NotificationMasterApi NotificationMasterApi;

    public TaskManager TaskManager;

    public ResidentialAethernet ResidentialAethernet;
    public CustomAethernet CustomAethernet;
    internal FollowPath followPath = null;
    public Provider IPCProvider;
    public static IDtrBarEntry? Entry;

    public FollowPath FollowPath
    {
        get
        {
            followPath ??= new();
            return followPath;
        }
    }
    public VnavmeshManager VnavmeshManager;
    public SplatoonManager SplatoonManager;
    public bool DisableHousePathData = false;
    public CharaSelectOverlay CharaSelectOverlay;
    private bool IsDev;
    public Lifestream(IDalamudPluginInterface pluginInterface)
    {
#if !DEBUG
        if (pluginInterface.IsDev || !pluginInterface.SourceRepository.Contains("Ookura-Risona/DalamudPlugins"))
        {
            IsDev = true;
            return;
        }
#endif
        P = this;
        ECommonsMain.Init(pluginInterface, this, Module.SplatoonAPI);
#if CUSTOMCS
        PluginLog.Warning($"Using custom FFXIVClientStructs");
        var gameVersion = DalamudReflector.TryGetDalamudStartInfo(out var ver) ? ver.GameVersion.ToString() : "unknown";
        InteropGenerator.Runtime.Resolver.GetInstance.Setup(Svc.SigScanner.SearchBase, gameVersion, new(Svc.PluginInterface.ConfigDirectory.FullName + "/cs.json"));
        FFXIVClientStructs.Interop.Generated.Addresses.Register();
        InteropGenerator.Runtime.Resolver.GetInstance.Resolve();
#endif
        new TickScheduler(delegate
        {
            TerritoryWatcher.Initialize();
            Config = EzConfig.Init<Config>();
            Utils.CheckConfigMigration();
            EzConfigGui.Init(MainGui.Draw);
            Overlay = new();
            TaskManager = new();
            TaskManager.DefaultConfiguration.ShowDebug = true;
            EzConfigGui.WindowSystem.AddWindow(Overlay);
            EzConfigGui.WindowSystem.AddWindow(new ProgressOverlay());
            CharaSelectOverlay = new();
            EzConfigGui.WindowSystem.AddWindow(CharaSelectOverlay);
            EzCmd.Add("/lifestream", ProcessCommand, "打开插件配置");
            EzCmd.Add("/li", ProcessCommand, """
                回到你的原始服务器
                /li <服务器名> - 前往指定的服务器
                /li <大区名> - 前往指定大区的随机服务器
                /li <以太水晶名称> - 如果您位于任何受支持的以太网络旁边，则前往指定的以太网络目的地

                /li <地址> - 前往当前服务器中的指定地块，其中地址 - 地块地址格式为“住宅区,房区,房号”格式（不带引号）
                /li <服务器> <地址> - 前往指定服务器的指定地址

                /li gc|hc - 前往你的大国防联军 (需安装 vnavmesh)
                /li gc|hc <大国防联军名字> - 前往指定大国防联军 (需安装 vnavmesh)
                /li gcc|hcc - 前往你大国防联军的部队箱 (需安装 vnavmesh)
                /li gcc|hcc <大国防联军名字> - 前往指定大国防联军的部队箱 (需安装 vnavmesh)
                ...其中“gc”或“gcc”将把你带到当前服务器的大国防联军，而“hc”或“hcc”将首先让你返回原始服务器

                /li auto - 前往您的个人房屋、部队房屋或公寓，无论在此顺序中找到什么
                /li home|house|private - 前往你的个人房屋
                /li fc|free|company|free company - 前往你的部队房屋
                /li apartment|apt - 前往你的公寓

                /li w|world|open|select - 打开跨服窗口
                /li island - 前往无人岛
                """);
            DataStore = new();
            ProperOnLogin.RegisterAvailable(() =>
            {
                DataStore.BuildWorlds();
                Config.CharaMap[Player.CID] = Player.NameWithWorld;
            });
            Svc.Framework.Update += Framework_Update;
            Memory = new();
            //EqualStrings.RegisterEquality("Guilde des aventuriers (Guildes des armuriers & forgeron...", "Guilde des aventuriers (Guildes des armuriers & forgerons/Maelstrom)");
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
            AutoRetainerApi = new();
            NotificationMasterApi = new(Svc.PluginInterface);
            ResidentialAethernet = new();
            CustomAethernet = new();
            VnavmeshManager = new();
            SplatoonManager = new();
            IPCProvider = new();
            SingletonServiceManager.Initialize(typeof(Service));
        });
    }

    private void Toasts_ErrorToast(ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
    {
        if(!Svc.ClientState.IsLoggedIn)
        {
            //430	60	8	0	False	Please wait and try logging in later.
            if(message.GetText().Trim() == Svc.Data.GetExcelSheet<LogMessage>().GetRow(430).Text.GetText().Trim())
            {
                PluginLog.Warning($"CharaSelectListMenuError encountered");
                EzThrottler.Throttle("CharaSelectListMenuError", 2.Minutes(), true);
            }
        }
    }

    internal void ProcessCommand(string command, string arguments)
    {
        if(arguments.StartsWith("debug TaskAetheryteAethernetTeleport "))
        {
            var args = arguments.Split(" ");
            if(args.Length == 4 && args[3] == "firmament")
            {
                args[3] = TaskAetheryteAethernetTeleport.FirmamentAethernetId.ToString();
            }
            if(args.Length != 4 || !uint.TryParse(args[2], out var a) || !uint.TryParse(args[3], out var b))
            {
                DuoLog.Error("Invalid arguments");
                return;
            }

            try
            {
                TaskAetheryteAethernetTeleport.Enqueue(a, b);
            }
            catch(Exception e)
            {
                DuoLog.Error(e.Message);
            }
        }
        else if(arguments == "debug WotsitManager clear")
        {
            S.WotsitManager.TryClearWotsit();
            Notify.Info("WotsitManager cleared, see logs for details");
        }
        else if(arguments == "debug WotsitManager init")
        {
            S.WotsitManager.MaybeTryInit();
            Notify.Info("WotsitManager reinitialized, see logs for details");
        }
        else if(arguments == "stop")
        {
            Notify.Info($"Discarding {TaskManager.NumQueuedTasks + (TaskManager.IsBusy ? 1 : 0)} tasks");
            TaskManager.Abort();
            followPath?.Stop();
            TabUtility.TargetWorldID = 0;
        }
        else if(arguments.Length == 1 && int.TryParse(arguments, out var val) && val.InRange(1, 9))
        {
            if(S.InstanceHandler.GetInstance() == val)
            {
                DuoLog.Warning($"Already in instance {val}");
            }
            else if(S.InstanceHandler.CanChangeInstance())
            {
                TaskChangeInstance.Enqueue(val);
            }
            else
            {
                DuoLog.Error($"Can't change instance now");
            }
        }
        else if(arguments.EqualsIgnoreCaseAny("open", "select", "window", "w", "world", "travel"))
        {
            S.SelectWorldWindow.IsOpen = true;
        }
        else if(arguments == "auto")
        {
            TaskPropertyShortcut.Enqueue(TaskPropertyShortcut.PropertyType.自动);
        }
        else if(arguments.EqualsIgnoreCaseAny("home", "house", "private"))
        {
            TaskPropertyShortcut.Enqueue(TaskPropertyShortcut.PropertyType.个人房屋);
        }
        else if(arguments.EqualsIgnoreCaseAny("fc", "free", "company", "company", "free company"))
        {
            TaskPropertyShortcut.Enqueue(TaskPropertyShortcut.PropertyType.部队房屋);
        }
        else if(arguments.EqualsIgnoreCaseAny("apartment", "apt"))
        {
            TaskPropertyShortcut.Enqueue(TaskPropertyShortcut.PropertyType.公寓);
        }
        else if(arguments.EqualsIgnoreCaseAny("shared"))
        {
            TaskPropertyShortcut.Enqueue(TaskPropertyShortcut.PropertyType.共享房屋);
        }
        else if(arguments.EqualsIgnoreCaseAny("inn", "hinn") || arguments.StartsWithAny(StringComparison.OrdinalIgnoreCase, "inn ", "hinn "))
        {
            var x = arguments.Split(" ");
            int? innNum = x.Length == 1 ? null : int.Parse(x[1]) - 1;
            if(innNum != null && !innNum.Value.InRange(0, TaskPropertyShortcut.InnData.Count))
            {
                var num = 1;
                DuoLog.Warning($"Invalid inn index. Valid inns are: \n{TaskPropertyShortcut.InnData.Select(s => $"{num++} - {Utils.GetInnNameFromTerritory(s.Key)}").Print("\n")}");
            }
            else
            {
                TaskPropertyShortcut.Enqueue(TaskPropertyShortcut.PropertyType.旅馆, innIndex: innNum, useSameWorld: !arguments.StartsWithAny("hinn"));
            }
        }
        else if(arguments.EqualsAny("gc", "gcc", "hc", "hcc", "fcgc", "gcfc") || arguments.StartsWithAny("gc ", "gcc ", "hc ", "hcc ", "fcgc ", "gcfc "))
        {
            var arglist = arguments.Split(" ");
            var isChest = arguments.StartsWithAny("gcc", "hcc");
            var fcgc = arguments.StartsWithAny("fcgc", "gcfc");
            var returnHome = arguments[0] == 'h';
            if(arglist.Length == 1)
            {
                TaskGCShortcut.Enqueue(null, isChest, returnHome, fcgc);
            }
            else
            {
                if(arglist[1].EqualsIgnoreCaseAny(GrandCompany.TwinAdder.ToString(), "Twin Adder", "Twin", "Adder", "TA", "A", "serpent"))
                {
                    TaskGCShortcut.Enqueue(GrandCompany.TwinAdder, isChest, returnHome, fcgc);
                }
                else if(arglist[1].EqualsIgnoreCaseAny(GrandCompany.Maelstrom.ToString(), "Mael", "S", "M", "storm", "strom"))
                {
                    TaskGCShortcut.Enqueue(GrandCompany.Maelstrom, isChest, returnHome, fcgc);
                }
                else if(arglist[1].EqualsIgnoreCaseAny(GrandCompany.ImmortalFlames.ToString(), "Immortal Flames", "Immortal", "Flames", "IF", "F", "flame"))
                {
                    TaskGCShortcut.Enqueue(GrandCompany.ImmortalFlames, isChest, returnHome, fcgc);
                }
                else if(Enum.TryParse<GrandCompany>(arglist[1], out var result))
                {
                    TaskGCShortcut.Enqueue(result, isChest, returnHome, fcgc);
                }
                else
                {
                    DuoLog.Error($"Could not parse input: {arglist[1]}");
                }
            }
        }
        else if(arguments.EqualsIgnoreCaseAny("mb", "market"))
        {
            if(!P.TaskManager.IsBusy && Player.Interactable)
            {
                TaskMBShortcut.Enqueue();
            }
        }
        else if(arguments.EqualsIgnoreCaseAny("island", "is", "sanctuary") || arguments.StartsWithAny("island ", "is ", "sanctuary "))
        {
            var arglist = arguments.Split(" ");
            if(arglist.Length == 1)
                TaskISShortcut.Enqueue();
            else
            {
                var name = arglist[1];
                if(DataStore.IslandNPCs.TryGetFirst(x => x.Value.Any(y => y.Contains(name, StringComparison.OrdinalIgnoreCase)), out var npc))
                    TaskISShortcut.Enqueue(npc.Key);
                else
                    DuoLog.Error($"Could not parse input: {name}");
            }
        }
        else if(arguments.StartsWithAny(StringComparison.OrdinalIgnoreCase, "tp"))
        {
            var destination = arguments[(arguments.IndexOf("tp") + 2)..].Trim();
            if(destination == null || destination == "")
            {
                DuoLog.Error($"Please type something");
            }
            else
            {
                if(!P.TaskManager.IsBusy && Player.Interactable)
                {
                    foreach(var x in Svc.AetheryteList.Where(s => s.AetheryteData.IsValid))
                    {
                        if(x.AetheryteData.Value.AethernetName.ToString().Contains(destination, StringComparison.OrdinalIgnoreCase))
                        {
                            if(S.TeleportService.TeleportToAetheryte(x.AetheryteId))
                            {
                                ChatPrinter.Green($"[Lifestream] Destination (Aethernet): {x.AetheryteData
                                    .Value.AethernetName.ValueNullable?.Name} at {ExcelTerritoryHelper.GetName(x.AetheryteData.Value.Territory.RowId)}");
                                return;
                            }
                        }
                    }
                    foreach(var x in Svc.AetheryteList.Where(s => s.AetheryteData.IsValid && s.AetheryteData.Value.PlaceName.IsValid))
                    {
                        if(x.AetheryteData.Value.PlaceName.Value.Name.ToString().Contains(destination, StringComparison.OrdinalIgnoreCase))
                        {
                            if(S.TeleportService.TeleportToAetheryte(x.AetheryteId))
                            {
                                ChatPrinter.Green($"[Lifestream] Destination (Place): {x.AetheryteData
                                    .Value.PlaceName.ValueNullable?.Name} at {ExcelTerritoryHelper.GetName(x.AetheryteData.Value.Territory.RowId)}");
                                return;
                            }
                        }
                    }
                    foreach(var x in Svc.AetheryteList.Where(s => s.AetheryteData.IsValid && s.AetheryteData.Value.Territory.IsValid && s.AetheryteData.Value.Territory.Value.PlaceName.IsValid))
                    {
                        if(x.AetheryteData.Value.Territory.Value.PlaceName.Value.Name.ToString().Contains(destination, StringComparison.OrdinalIgnoreCase))
                        {
                            if(S.TeleportService.TeleportToAetheryte(x.AetheryteId))
                            {
                                ChatPrinter.Green($"[Lifestream] Destination (Zone): {x.AetheryteData
                                    .Value.Territory.Value.PlaceName.Value.Name} at {ExcelTerritoryHelper.GetName(x.AetheryteData.Value.Territory.RowId)}");
                                return;
                            }
                        }
                    }
                    DuoLog.Error($"Could not parse {destination}");
                }
            }

        }
        else if(Utils.TryParseAddressBookEntry(arguments, out var entry))
        {
            ChatPrinter.Green($"[Lifestream] Address parsed: {entry.GetAddressString()}");
            entry.GoTo();
        }
        else
        {
            if(command.EqualsIgnoreCase("/lifestream") && arguments == "")
            {
                EzConfigGui.Open();
            }
            else
            {
                foreach(var b in Config.AddressBookFolders)
                {
                    foreach(var e in b.Entries)
                    {
                        if(e.AliasEnabled && e.Alias != "" && e.Alias.EqualsIgnoreCase(arguments))
                        {
                            e.GoTo();
                            return;
                        }
                    }
                }
                foreach(var x in Config.CustomAliases)
                {
                    if(!x.Enabled || x.Alias == "") continue;
                    if(x.Alias.EqualsIgnoreCase(arguments))
                    {
                        x.Enqueue();
                        return;
                    }
                }

                var argsSplit = arguments.Split(' ');
                var primary = arguments.Split(' ').SafeSelect(0);
                var additionalCommand = argsSplit.Length > 1 ? argsSplit[1..].Join(" ") : null;
                WorldChangeAetheryte? gateway = null;
                if(additionalCommand == "mb")
                {
                    gateway = WorldChangeAetheryte.Uldah;
                }

                if(DataStore.Worlds.TryGetFirst(x => x.StartsWith(primary == "" ? Player.HomeWorld : primary, StringComparison.OrdinalIgnoreCase), out var w))
                {
                    PluginLog.Information($"Same dc/{primary}/{w}");
                    TPAndChangeWorld(w, false, gateway: gateway);
                }
                else if(DataStore.DCWorlds.TryGetFirst(x => x.StartsWith(primary == "" ? Player.HomeWorld : primary, StringComparison.OrdinalIgnoreCase), out var dcw))
                {
                    PluginLog.Information($"Cross dc/{primary}/{w}");
                    TPAndChangeWorld(dcw, true, gateway: gateway);
                }
                else if(Utils.TryGetWorldFromDataCenter(primary, out var world, out var dc))
                {
                    Utils.DisplayInfo($"Random world from {Svc.Data.GetExcelSheet<WorldDCGroupType>().GetRow(dc).Name}: {world}");
                    TPAndChangeWorld(world, Player.Object.CurrentWorld.ValueNullable?.DataCenter.RowId != dc, gateway: gateway);
                }
                else
                {
                    TaskTryTpToAethernetDestination.Enqueue(primary);
                }

                if(additionalCommand != null)
                {
                    TaskManager.Enqueue(() => IsScreenReady() && Player.Interactable);
                    TaskManager.Enqueue(() => Svc.Framework.RunOnTick(() => Svc.Commands.ProcessCommand($"/li {additionalCommand}"), delayTicks:1));
                }
            }
        }
    }

    internal void TPAndChangeWorld(string destinationWorld, bool isDcTransfer = false, string secondaryTeleport = null, bool noSecondaryTeleport = false, WorldChangeAetheryte? gateway = null, bool? doNotify = null, bool? returnToGateway = null, bool skipChecks = false)
    {
        try
        {
            Utils.AssertCanTravel(Player.Name, Player.Object.HomeWorld.RowId, Player.Object.CurrentWorld.RowId, destinationWorld);
            CharaSelectVisit.ApplyDefaults(ref returnToGateway, ref gateway, ref doNotify);
            if(!skipChecks)
            {
                if(isDcTransfer && !P.Config.AllowDcTransfer)
                {
                    Notify.Error($"Data center transfers are not enabled in the configuration.");
                    return;
                }
                if(TaskManager.IsBusy)
                {
                    Notify.Error("Another task is in progress");
                    return;
                }
            }
            if(!Player.Available)
            {
                Notify.Error("No player");
                return;
            }
            if(destinationWorld == Player.CurrentWorld)
            {
                Notify.Error("Already in this world");
                return;
            }
            /*if(ActionManager.Instance()->GetActionStatus(ActionType.Spell, 5) != 0)
            {
                Notify.Error("You are unable to teleport at this time");
                return;
            }*/
            if(Svc.Party.Length > 1 && !P.Config.LeavePartyBeforeWorldChange && !P.Config.LeavePartyBeforeWorldChange)
            {
                Notify.Warning("You must disband party in order to switch worlds");
            }
            Utils.DisplayInfo($"Destination: {destinationWorld}");
            if(isDcTransfer)
            {
                var type = DCVType.Unknown;
                var homeDC = Player.Object.HomeWorld.ValueNullable?.DataCenter.ValueNullable?.Name.ToString() ?? throw new NullReferenceException("Home DC is null ??????");
                var currentDC = Player.Object.CurrentWorld.ValueNullable?.DataCenter.ValueNullable?.Name.ToString() ?? throw new NullReferenceException("Current DC is null ??????"); ;
                var targetDC = Utils.GetDataCenterName(destinationWorld);
                if(currentDC == homeDC)
                {
                    type = DCVType.HomeToGuest;
                }
                else
                {
                    if(targetDC == homeDC)
                    {
                        type = DCVType.GuestToHome;
                    }
                    else
                    {
                        type = DCVType.GuestToGuest;
                    }
                }
                TaskRemoveAfkStatus.Enqueue();
                if(type != DCVType.Unknown)
                {
                    if(Config.TeleportToGatewayBeforeLogout && !(TerritoryInfo.Instance()->InSanctuary || ExcelTerritoryHelper.IsSanctuary(P.Territory)) && !(currentDC == homeDC && Player.HomeWorld != Player.CurrentWorld))
                    {
                        TaskTpToAethernetDestination.Enqueue(gateway.Value.AdjustGateway());
                    }
                    if(Config.LeavePartyBeforeLogout)
                    {
                        if(Svc.Party.Length > 1 || Svc.Condition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance])
                        {
                            TaskManager.EnqueueTask(new(WorldChange.LeaveAnyParty));
                        }
                    }
                }
                if(type == DCVType.HomeToGuest)
                {
                    if(!Player.IsInHomeWorld) TaskTPAndChangeWorld.Enqueue(Player.HomeWorld, gateway.Value.AdjustGateway(), false);
                    TaskWaitUntilInHomeWorld.Enqueue();
                    TaskLogoutAndRelog.Enqueue(Player.NameWithWorld);
                    CharaSelectVisit.HomeToGuest(destinationWorld, Player.Name, Player.Object.HomeWorld.RowId, secondaryTeleport, noSecondaryTeleport, gateway, doNotify, returnToGateway);
                }
                else if(type == DCVType.GuestToHome)
                {
                    TaskLogoutAndRelog.Enqueue(Player.NameWithWorld);
                    CharaSelectVisit.GuestToHome(destinationWorld, Player.Name, Player.Object.HomeWorld.RowId, secondaryTeleport, noSecondaryTeleport, gateway, doNotify, returnToGateway);
                }
                else if(type == DCVType.GuestToGuest)
                {
                    TaskLogoutAndRelog.Enqueue(Player.NameWithWorld);
                    CharaSelectVisit.GuestToGuest(destinationWorld, Player.Name, Player.Object.HomeWorld.RowId, secondaryTeleport, noSecondaryTeleport, gateway, doNotify, returnToGateway);
                }
                else
                {
                    DuoLog.Error($"Error - unknown data center visit type");
                }
                PluginLog.Information($"Data center visit: {type}");
            }
            else
            {
                TaskRemoveAfkStatus.Enqueue();
                /*if(Config.LeavePartyBeforeWorldChangeSameWorld && (Svc.Party.Length > 1 || Svc.Condition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance]))
                {
                    TaskManager.EnqueueTask(new(WorldChange.LeaveAnyParty));
                }*/
                TaskTPAndChangeWorld.Enqueue(destinationWorld, gateway.Value.AdjustGateway(), false);
                if(doNotify == true) TaskDesktopNotification.Enqueue($"Arrived to {destinationWorld}");
                CharaSelectVisit.EnqueueSecondary(noSecondaryTeleport, secondaryTeleport);
            }
        }
        catch(Exception e)
        {
            e.Log();
        }
    }

    private void Framework_Update(object framework)
    {
        YesAlreadyManager.Tick();
        followPath?.Update();
        if(Svc.ClientState.LocalPlayer != null && DataStore.Territories.Contains(P.Territory))
        {
            UpdateActiveAetheryte();
        }
        else
        {
            ActiveAetheryte = null;
        }
        ResidentialAethernet.Tick();
        CustomAethernet.Tick();
        if(!Svc.ClientState.IsLoggedIn)
        {
            if(TryGetAddonMaster<AddonMaster._CharaSelectListMenu>(out var m) && m.IsAddonReady)
            {
                foreach(var chara in m.Characters)
                {
                    Config.CharaMap[chara.Entry->ContentId] = $"{chara.Name}@{ExcelWorldHelper.GetName(chara.HomeWorld)}";
                }
            }
        }
        if(P.TaskManager.IsBusy)
        {
            if(EzThrottler.Throttle("EnsureEnhancedLoginIsOff")) Utils.EnsureEnhancedLoginIsOff();
            if(TryGetAddonByName<AtkUnitBase>("Trade", out var trade))
            {
                Callback.Fire(trade, true, -1);
            }
        }
        if(TabUtility.TargetWorldID != 0)
        {
            if(Player.Available && Player.Object.CurrentWorld.RowId == TabUtility.TargetWorldID && IsScreenReady())
            {
                if(EzThrottler.Throttle("TerminateGame", 60000))
                {
                    Environment.Exit(0);
                }
                else
                {
                    if(EzThrottler.Throttle("WarnTerminate", 1000))
                    {
                        DuoLog.Warning($"Arrived to {ExcelWorldHelper.GetName(TabUtility.TargetWorldID)}. Game is shutting down in {EzThrottler.GetRemainingTime("TerminateGame") / 1000} seconds. Type \"/li stop\" to cancel.");
                    }
                }
            }
            else
            {
                EzThrottler.Throttle("TerminateGame", 60000, true);
            }
        }
    }

    public void Dispose()
    {
#if !DEBUG
        if (IsDev)
        {
            return;
        }
#endif
        Svc.Framework.Update -= Framework_Update;
        Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
        Memory.Dispose();
        followPath?.Dispose();
        ECommonsMain.Dispose();
        P = null;
    }

    private void UpdateActiveAetheryte()
    {
        var a = Utils.GetValidAetheryte();
        if(a != null)
        {
            var pos2 = a.Position.ToVector2();
            foreach(var x in DataStore.Aetherytes)
            {
                if(x.Key.TerritoryType == P.Territory && Vector2.Distance(x.Key.Position, pos2) < 10)
                {
                    if(ActiveAetheryte == null)
                    {
                        Overlay.IsOpen = true;
                    }
                    ActiveAetheryte = x.Key;
                    return;
                }
                foreach(var l in x.Value)
                {
                    if(l.TerritoryType == P.Territory && Vector2.Distance(l.Position, pos2) < 10)
                    {
                        if(ActiveAetheryte == null)
                        {
                            Overlay.IsOpen = true;
                        }
                        ActiveAetheryte = l;
                        return;
                    }
                }
            }
        }
        else
        {
            ActiveAetheryte = null;
        }
        if(!Overlay.IsOpen)
        {
            if(P.Config.ShowInstanceSwitcher && S.InstanceHandler.GetInstance() != 0 && TaskChangeInstance.GetAetheryte() == null && ActiveAetheryte == null)
            {
                Overlay.IsOpen = true;
            }
        }
    }
}