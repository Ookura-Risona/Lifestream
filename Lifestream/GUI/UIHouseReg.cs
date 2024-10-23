using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.GameHelpers;
using ECommons.SplatoonAPI;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lifestream.Data;
using Lifestream.Enums;
using NightmareUI;
using NightmareUI.PrimaryUI;

namespace Lifestream.GUI;
#nullable enable
public static unsafe class UIHouseReg
{
    public static void Draw()
    {
        if(Player.Available)
        {
            NuiTools.ButtonTabs([[new("个人房屋", DrawPrivate), new("部队房屋", DrawFC)]]);
        }
        else
        {
            ImGuiEx.TextWrapped("请登录才能使用此功能。");
        }
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
            ImGuiEx.TextWrapped(ImGuiColors.ParsedGreen, $"{data.ResidentialDistrict.GetName()}, {data.Ward + 1}区, {data.Plot + 1}号 已被注册为 {(data.IsPrivate ? "个人" : "部队")}房屋。");
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
            if(data.ResidentialDistrict == kind && data.Ward == ward && data.Plot == plot)
            {
                var path = data.PathToEntrance;
                new NuiBuilder()
                    .Section("通往房子的路")
                    .Widget(() =>
                    {
                        ImGuiEx.TextWrapped($"创建从地块入口到房屋入口的路径。一条路径的第一个点应该稍微在你的地块内，你可以在传送后直线跑到那里，最后一个点应该靠近房子入口，你可以从那里进入房子。");

                        ImGui.PushID($"path{isPrivate}");
                        DrawPathEditor(path, data);
                        ImGui.PopID();

                    }).Draw();
            }
            else
            {
                ImGuiEx.TextWrapped("进入地块范围内来编辑路径");
            }
        }
    }

    public static void DrawPathEditor(List<Vector3> path, HousePathData? data = null)
    {
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
                if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Play, "测试", data.ResidentialDistrict.GetResidentialTerritory() == Player.Territory && Vector3.Distance(Player.Position, entryPoint.Value) < 10f))
                {
                    P.FollowPath.Move(data.PathToEntrance, true);
                }
                if(ImGui.IsItemHovered())
                {
                    ImGuiEx.Tooltip($"""
                        住宅区区域: {data.ResidentialDistrict.GetResidentialTerritory()}
                        玩家区域: {Player.Territory}
                        到入口点的距离: {Vector3.Distance(Player.Position, entryPoint.Value)}
                        """);
                }
            }
        }
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
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"{i + 1}");
                ImGui.TableNextColumn();
                if(ImGui.ArrowButton("##up", ImGuiDir.Up) && i > 0)
                {
                    (path[i - 1], path[i]) = (path[i], path[i - 1]);
                }
                Visualise();
                ImGui.SameLine();
                if(ImGui.ArrowButton("##down", ImGuiDir.Down) && i < path.Count - 1)
                {
                    (path[i - 1], path[i]) = (path[i], path[i - 1]);
                }
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

        P.SplatoonManager.RenderPath(path, false, true);
    }

    private static bool IsOutside()
    {
        return P.ResidentialAethernet.ZoneInfo.ContainsKey(Svc.ClientState.TerritoryType);
    }

    private static bool IsInsideHouse()
    {
        return Svc.ClientState.TerritoryType.EqualsAny(
            Houses.Private_Cottage_Mist, Houses.Private_House_Mist, Houses.Private_Mansion_Mist,
            Houses.Private_Cottage_Empyreum, Houses.Private_House_Empyreum, Houses.Private_Mansion_Empyreum,
            Houses.Private_Cottage_Shirogane, Houses.Private_House_Shirogane, Houses.Private_Mansion_Shirogane,
            Houses.Private_Cottage_The_Goblet, Houses.Private_House_The_Goblet, Houses.Private_Mansion_The_Goblet,
            Houses.Private_Cottage_The_Lavender_Beds, Houses.Private_House_The_Lavender_Beds, Houses.Private_Mansion_The_Lavender_Beds
            );
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
            kind = Utils.GetResidentialAetheryteByTerritoryType(Svc.ClientState.TerritoryType) ?? 0;
            return kind != 0;
        }
        kind = default;
        ward = default;
        plot = default;
        return false;
    }
}
