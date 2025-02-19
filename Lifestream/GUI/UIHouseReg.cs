using ECommons;
using ECommons.Configuration;
using ECommons.ExcelServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.GameHelpers;
using ECommons.Reflection;
using ECommons.SplatoonAPI;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lifestream.Data;
using Lifestream.Enums;
using Lifestream.Services;
using Lumina.Excel.Sheets;
using NightmareUI;
using NightmareUI.ImGuiElements;
using NightmareUI.PrimaryUI;

namespace Lifestream.GUI;
#nullable enable
public static unsafe class UIHouseReg
{
    public static ImGuiEx.RealtimeDragDrop<Vector3> PathDragDrop = new("UIHouseReg", (x) => x.ToString());

    public static void Draw()
    {
        if(Player.Available)
        {
            NuiTools.ButtonTabs([[new("个人房屋", DrawPrivate), new("部队房屋", DrawFC), new("Custom House", DrawCustom), new("总览", DrawOverview)]]);
        }
        else
        {
            ImGuiEx.TextWrapped("请登录才能使用此功能。");
            DrawOverview();
        }
    }

    private static ImGuiEx.RealtimeDragDrop<(ulong CID, HousePathData? Private, HousePathData? FC)> DragDropPathData = new("DragDropHPD", (x) => x.CID.ToString());
    private static string Search = "";
    private static int World = 0;
    private static WorldSelector WorldSelector = new()
    {
        DisplayCurrent = true,
        ShouldHideWorld = (x) => !P.Config.HousePathDatas.Any(s => Utils.GetWorldFromCID(s.CID) == ExcelWorldHelper.GetName(x)),
        EmptyName = "All Worlds",
        DefaultAllOpen = true,
    };

    private static void DrawOverview()
    {
        ImGuiEx.InputWithRightButtonsArea(() =>
        {
            ImGui.InputTextWithHint("##search", "搜索...", ref Search, 50);
        }, () =>
        {
            ImGui.SetNextItemWidth(200f);
            WorldSelector.Draw(ref World);
        });
        List<(ulong CID, HousePathData? Private, HousePathData? FC)> charaDatas = [];
        foreach(var x in P.Config.HousePathDatas.Select(x => x.CID).Distinct())
        {
            charaDatas.Add((x, P.Config.HousePathDatas.FirstOrDefault(z => z.IsPrivate && z.CID == x), P.Config.HousePathDatas.FirstOrDefault(z => !z.IsPrivate && z.CID == x)));
        }
        DragDropPathData.Begin();
        if(ImGuiEx.BeginDefaultTable("##charaTable", ["##move", "~Name or CID", "Private", "##privateCtl", "##privateCtl2", "##privateDlm", "FC", "##FCCtl", "Workshop", "##workshopCtl", "##fcCtl", "##fcCtl2"]))
        {
            for(var i = 0; i < charaDatas.Count; i++)
            {
                var charaData = charaDatas[i];
                var charaName = Utils.GetCharaName(charaData.CID);
                if(Search != "" && !charaName.Contains(Search, StringComparison.OrdinalIgnoreCase)) continue;
                if(World != 0 && Utils.GetWorldFromCID(charaData.CID) != ExcelWorldHelper.GetName(World)) continue;
                ImGui.PushID($"{charaData}");
                var priv = charaData.Private;
                var fc = charaData.FC;
                var entry = (priv ?? fc)!;
                ImGui.TableNextRow();
                DragDropPathData.SetRowColor(entry.CID.ToString());
                ImGui.TableNextColumn();
                DragDropPathData.NextRow();
                DragDropPathData.DrawButtonDummy(charaData.CID.ToString(), charaDatas, i);
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"{charaName}");
                ImGui.TableNextColumn();
                if(priv != null)
                {
                    NuiTools.RenderResidentialIcon((uint)priv.ResidentialDistrict.GetResidentialTerritory());
                    ImGui.SameLine();
                    ImGuiEx.Text($"{priv.Ward + 1}区, {priv.Plot + 1}号{(priv.PathToEntrance.Count > 0 ? ", +路径" : "")}");
                    ImGui.TableNextColumn();
                    if(ImGuiEx.IconButton((FontAwesomeIcon)'\ue50b', "DelePrivate", enabled: ImGuiEx.Ctrl))
                    {
                        new TickScheduler(() => P.Config.HousePathDatas.RemoveAll(z => z.IsPrivate && z.CID == charaData.CID));
                    }
                    ImGuiEx.Tooltip("按住 CTRL + 单击删除注册私人房屋。");
                    if(priv.PathToEntrance.Count > 0)
                    {
                        ImGui.SameLine();
                        if(ImGuiEx.IconButton((FontAwesomeIcon)'\ue566', "DelePrivatePath", enabled: ImGuiEx.Ctrl))
                        {
                            priv.PathToEntrance.Clear();
                        }
                        ImGuiEx.Tooltip("按住 CTRL + 单击删除到私人房屋的路径。");
                    }

                    ImGui.SameLine();
                    if(ImGuiEx.IconButton(FontAwesomeIcon.Copy, "CopyPrivatePath"))
                    {
                        Copy(EzConfig.DefaultSerializationFactory.Serialize(priv)!);
                    }
                    ImGuiEx.Tooltip("Copy private registration data to clipboard");
                    ImGui.SameLine();
                }
                else
                {
                    ImGuiEx.TextV(ImGuiColors.DalamudGrey3, "未注册");
                    ImGui.TableNextColumn();
                }

                ImGui.TableNextColumn();

                if(ImGuiEx.IconButton(FontAwesomeIcon.Paste, "PastePriva"))
                {
                    ImportFromClipboard(charaData.CID, true);
                }
                ImGuiEx.Tooltip("Paste private registration data from clipboard");

                ImGui.TableNextColumn();
                //delimiter
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetStyle().Colors[(int)ImGuiCol.TableBorderLight].ToUint());

                ImGui.TableNextColumn();
                if(fc != null)
                {
                    NuiTools.RenderResidentialIcon((uint)fc.ResidentialDistrict.GetResidentialTerritory());
                    ImGui.SameLine();
                    ImGuiEx.Text($"W{fc.Ward + 1}, P{fc.Plot + 1}{(fc.PathToEntrance.Count > 0 ? ", +path" : "")}");
                    ImGui.TableNextColumn();
                    if(ImGuiEx.IconButton((FontAwesomeIcon)'\ue50b', "DeleFc", enabled: ImGuiEx.Ctrl))
                    {
                        new TickScheduler(() => P.Config.HousePathDatas.RemoveAll(z => !z.IsPrivate && z.CID == charaData.CID));
                    }
                    ImGuiEx.Tooltip("按住 CTRL + 单击取消注册部队房屋。");
                    if(fc.PathToEntrance.Count > 0)
                    {
                        ImGui.SameLine();
                        if(ImGuiEx.IconButton((FontAwesomeIcon)'\ue566', "DeleFcPath", enabled: ImGuiEx.Ctrl))
                        {
                            fc.PathToEntrance.Clear();
                        }
                        ImGuiEx.Tooltip("按住 CTRL + 单击删除到部队房屋的路径。");
                    }
                }
                else
                {
                    ImGuiEx.TextV(ImGuiColors.DalamudGrey3, "未注册");
                    ImGui.TableNextColumn();
                }

                ImGui.TableNextColumn();
                if(fc == null || fc.PathToWorkshop.Count == 0)
                {
                    ImGuiEx.TextV(ImGuiColors.DalamudGrey3, "未注册");
                    ImGui.TableNextColumn();
                }
                else
                {
                    ImGuiEx.TextV($"{fc.PathToWorkshop.Count} points");
                    ImGui.TableNextColumn();
                    if(ImGuiEx.IconButton((FontAwesomeIcon)'\ue566', "DeleFcWorkshopPath", enabled:ImGuiEx.Ctrl))
                    {
                        fc.PathToWorkshop.Clear();
                    }
                    ImGuiEx.Tooltip("按住 CTRL + 单击删除到工房的路径。");
                }

                ImGui.TableNextColumn();

                if(fc != null)
                {
                    if(ImGuiEx.IconButton(FontAwesomeIcon.Copy, "CopyFCPath"))
                    {
                        Copy(EzConfig.DefaultSerializationFactory.Serialize(fc)!);
                    }
                    ImGuiEx.Tooltip("Copy free company registration data to clipboard");
                    ImGui.SameLine();
                }

                ImGui.TableNextColumn();
                if(ImGuiEx.IconButton(FontAwesomeIcon.Paste, "PasteFC"))
                {
                    ImportFromClipboard(charaData.CID, false);
                }
                ImGuiEx.Tooltip("Paste free company registration data from clipboard");
                ImGui.PopID();
            }

            ImGui.EndTable();
            DragDropPathData.End();
        }
        P.Config.HousePathDatas.Clear();
        foreach(var x in charaDatas)
        {
            if(x.Private != null) P.Config.HousePathDatas.Add(x.Private);
            if(x.FC != null) P.Config.HousePathDatas.Add(x.FC);
        }
    }

    static void ImportFromClipboard(ulong cid, bool isPrivate)
    {
        new TickScheduler(() =>
        {
            try
            {
                var data = EzConfig.DefaultSerializationFactory.Deserialize<HousePathData>(Paste()!) ?? throw new NullReferenceException("No suitable data forund in clipboard");
                if(!data.GetType().GetFieldPropertyUnions().All(x => x.GetValue(data) != null)) throw new NullReferenceException("Clipboard contains invalid data");
                var existingData = P.Config.HousePathDatas.FirstOrDefault(x => x.CID == cid && x.IsPrivate == isPrivate);
                var same = existingData != null && existingData.Ward == data.Ward && existingData.Plot == data.Plot && existingData.ResidentialDistrict == data.ResidentialDistrict;
                if(same || ImGuiEx.Ctrl)
                {
                    data.CID = cid;
                    var index = P.Config.HousePathDatas.IndexOf(s => s.CID == data.CID && s.IsPrivate == isPrivate);
                    if(index == -1)
                    {
                        P.Config.HousePathDatas.Add(data);
                    }
                    else
                    {
                        P.Config.HousePathDatas[index] = data;
                    }
                }
                else
                {
                    Notify.Error($"A different {(isPrivate?"private house plot":"FC house plot")} is already registered for this character. If you want to override it, hold CTRL and click paste button.");
                }
            }
            catch(Exception e)
            {
                Notify.Error(e.Message);
                e.Log();
            }
        });
    }

    private static void DrawFC()
    {
        var data = Utils.GetFCPathData();
        DrawHousingData(data, false);
    }

    private static void DrawPrivate()
    {
        var data = Utils.GetPrivatePathData();
        DrawHousingData(data, true);
    }

    private static void DrawCustom()
    {
        if(TryGetCurrentPlotInfo(out var kind, out var ward, out var plot))
        {
            if(P.Config.HousePathDatas.TryGetFirst(x => x.ResidentialDistrict == kind && x.Ward == ward && x.Plot == plot, out var regData))
            {
                ImGuiEx.TextWrapped($"This house is already registered as {(regData.IsPrivate ? "private house" : "FC house")} for character {Utils.GetCharaName(regData.CID)} and can not be registered as a custom house.");
            }
            else
            {
                var data = P.Config.CustomHousePathDatas.FirstOrDefault(x => x.Ward == ward && x.Plot == plot && x.ResidentialDistrict == kind);
                if(data == null)
                {
                    if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "Register this house as custom house"))
                    {
                        P.Config.CustomHousePathDatas.Add(new()
                        {
                            ResidentialDistrict = kind,
                            Plot = plot,
                            Ward = ward
                        });
                    }
                }
                else
                {
                    if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Trash, "Unregister this house", ImGuiEx.Ctrl))
                    {
                        new TickScheduler(() => P.Config.CustomHousePathDatas.Remove(data));
                    }
                    DrawHousingData_DrawPath(data, false, kind, ward, plot);
                }
            }
        }
        else
        {
            ImGuiEx.TextWrapped($"Please navigate to the plot to register it as custom house. Registering custom house will allow it's path to be used for shared estate teleports and address book teleports.");
        }
    }

    private static void DrawHousingData(HousePathData? data, bool isPrivate)
    {
        var plotDataAvailable = TryGetCurrentPlotInfo(out var kind, out var ward, out var plot);
        if(data == null)
        {
            ImGuiEx.Text($"没有找到数据。 ");
            if(plotDataAvailable && Player.IsInHomeWorld)
            {
                if(ImGui.Button($"注册 {kind.GetName()}, {ward + 1}区, {plot + 1}号 作为 {(isPrivate ? "个人" : "部队")} 房屋。"))
                {
                    var newData = new HousePathData()
                    {
                        CID = Player.CID,
                        Plot = plot,
                        Ward = ward,
                        ResidentialDistrict = kind,
                        IsPrivate = isPrivate
                    };
                    P.Config.HousePathDatas.Add(newData);
                }
            }
            else
            {
                ImGuiEx.Text($"前往您的地块注册数据。");
            }
        }
        else
        {
            ImGuiEx.TextWrapped(ImGuiColors.ParsedGreen, $"{data.ResidentialDistrict.GetName()}, {data.Ward + 1}区, {data.Plot + 1}号 已被注册为 {(data.IsPrivate ? "个人" : "部队")} 房屋。");
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Trash, "取消注册", ImGuiEx.Ctrl))
            {
                P.Config.HousePathDatas.Remove(data);
            }
            ImGui.Checkbox("传送后的行为", ref data.EnableHouseEnterModeOverride);
            if(data.EnableHouseEnterModeOverride)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGuiEx.EnumCombo("##override", ref data.EnterModeOverride);
            }
            DrawHousingData_DrawPath(data, isPrivate, kind, ward, plot);
        }
    }

    public static void DrawHousingData_DrawPath(HousePathData data, bool isPrivate, ResidentialAetheryteKind kind, int ward, int plot)
    {
        if(data.ResidentialDistrict == kind && data.Ward == ward && data.Plot == plot)
        {
            if(!Utils.IsInsideHouse())
            {
                var path = data.PathToEntrance;
                new NuiBuilder()
                    .Section("通往房子的路径")
                    .Widget(() =>
                    {
                        ImGuiEx.TextWrapped($"创建从地块入口到房屋入口的路径。一条路径的第一个点应该稍微在你的地块内，你可以在传送后直线跑到那里，最后一个点应该靠近房子入口，你可以从那里进入房子。");

                        ImGui.PushID($"path{isPrivate}");
                        DrawPathEditor(path, data);
                        ImGui.PopID();

                    }).Draw();
            }
            else if(!isPrivate)
            {
                var path = data.PathToWorkshop;
                new NuiBuilder()
                    .Section("通往工房的路径")
                    .Widget(() =>
                    {
                        ImGuiEx.TextWrapped($"创建从房屋入口到工房/私人房间入口的路径。");

                        ImGui.PushID($"workshop");
                        DrawPathEditor(path, data);
                        ImGui.PopID();

                    }).Draw();
            }
            else
            {
                ImGuiEx.TextWrapped("进入地块范围内来编辑路径");
            }
        }
        else
        {
            ImGuiEx.TextWrapped("Go to registered plot to edit path");
        }
    }

    public static void DrawPathEditor(List<Vector3> path, HousePathData? data = null)
    {
        if(!TerritoryWatcher.IsDataReliable())
        {
            ImGuiEx.Text(EColor.RedBright, $"You can not edit house path right now. \nPlease exit and enter your house.");
            return;
        }
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "添加到列表末尾"))
        {
            path.Add(Player.Position);
        }
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "添加到列表的开头"))
        {
            path.Insert(0, Player.Position);
        }
        if(data != null)
        {
            var entryPoint = Utils.GetPlotEntrance(data.ResidentialDistrict.GetResidentialTerritory(), data.Plot);
            if(entryPoint != null)
            {
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Play, "测试", data.ResidentialDistrict.GetResidentialTerritory() == P.Territory && Vector3.Distance(Player.Position, entryPoint.Value) < 10f))
                {
                    P.FollowPath.Move(data.PathToEntrance, true);
                }
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Play, "测试工房", data.PathToWorkshop.Count > 0 && Utils.IsInsideHouse()))
                {
                    P.FollowPath.Move(data.PathToWorkshop, true);
                }
                if(ImGui.IsItemHovered())
                {
                    ImGuiEx.Tooltip($"""
                        住宅区区域: {data.ResidentialDistrict.GetResidentialTerritory()}
                        玩家区域: {P.Territory}
                        到入口点的距离: {Vector3.Distance(Player.Position, entryPoint.Value)}
                        """);
                }
            }
        }
        PathDragDrop.Begin();
        if(ImGui.BeginTable($"pathtable", 4, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##num");
            ImGui.TableSetupColumn("##move");
            ImGui.TableSetupColumn("坐标", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##control");
            ImGui.TableHeadersRow();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGuiEx.Text($"地块入口");

            for(var i = 0; i < path.Count; i++)
            {
                ImGui.PushID($"point{i}");
                var p = path[i];
                ImGui.TableNextRow();
                PathDragDrop.SetRowColor(p.ToString());
                ImGui.TableNextColumn();
                PathDragDrop.NextRow();
                ImGuiEx.TextV($"{i + 1}");
                ImGui.TableNextColumn();
                PathDragDrop.DrawButtonDummy(p, path, i);
                Visualise();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"{p:F1}");
                Visualise();

                ImGui.TableNextColumn();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.MapPin, "到我的位置"))
                {
                    path[i] = Player.Position;
                }
                Visualise();
                ImGui.SameLine();
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Trash, "删除", ImGuiEx.Ctrl))
                {
                    var toRem = i;
                    new TickScheduler(() => path.RemoveAt(toRem));
                }
                Visualise();
                ImGui.PopID();

                void Visualise()
                {
                    if(ImGui.IsItemHovered() && Splatoon.IsConnected())
                    {
                        var e = new Element(ElementType.CircleAtFixedCoordinates);
                        e.SetRefCoord(p);
                        e.Filled = false;
                        e.thicc = 2f;
                        e.radius = (Environment.TickCount64 % 1000f / 1000f) * 2f;
                        Splatoon.DisplayOnce(e);
                    }
                }
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGuiEx.Text($"房子的入口");

            ImGui.EndTable();
        }
        PathDragDrop.End();

        P.SplatoonManager.RenderPath(path, false, true);
    }

    private static bool IsOutside()
    {
        return P.ResidentialAethernet.ZoneInfo.ContainsKey(P.Territory);
    }

    public static bool TryGetCurrentPlotInfo(out ResidentialAetheryteKind kind, out int ward, out int plot)
    {
        var h = HousingManager.Instance();
        if(h != null)
        {
            ward = h->GetCurrentWard();
            plot = h->GetCurrentPlot();
            if(ward < 0 || plot < 0)
            {
                kind = default;
                return false;
            }
            kind = Utils.GetResidentialAetheryteByTerritoryType(P.Territory) ?? 0;
            return kind != 0;
        }
        kind = default;
        ward = default;
        plot = default;
        return false;
    }
}
