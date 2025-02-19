using ECommons.GameHelpers;
using Lifestream.Data;
using NightmareUI;
using NightmareUI.ImGuiElements;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lifestream.GUI;
public static class TabTravelBan
{
    public static void Draw()
    {
        WorldSelector.Instance.DisplayCurrent = true;
        ImGui.PushFont(UiBuilder.IconFont);
        ImGuiEx.Text(EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGuiEx.TextWrapped(EColor.RedBright, "请注意，此功能是避免不可恢复错误的最后机会。使用此功能可能会破坏依赖 Lifestream 的其他插件。阻止特定方向的旅行只会阻止通过 Lifestream 进行的旅行。您仍然可以手动出行。");

        ImGuiEx.LineCentered(() =>
        {
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "添加新条目"))
            {
                var entry = new TravelBanInfo();
                if(Player.Available)
                {
                    entry.CharaName = Player.Name;
                    entry.CharaHomeWorld = (int)Player.Object.HomeWorld.RowId;
                }
                P.Config.TravelBans.Add(entry);
            }
        });
        if(ImGui.BeginTable("Bantable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("##enabled");
            ImGui.TableSetupColumn("角色名称和服务器", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("旅行出发地");
            ImGui.TableSetupColumn("旅行目的地");
            ImGui.TableSetupColumn("##control");

            ImGui.TableHeadersRow();
            for(var i = 0; i < P.Config.TravelBans.Count; i++)
            {
                var entry = P.Config.TravelBans[i];
                ImGui.PushID(entry.ID);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Checkbox("##en", ref entry.IsEnabled);
                ImGui.TableNextColumn();
                ImGuiEx.InputWithRightButtonsArea(() =>
                {
                    ImGui.InputTextWithHint("##chara", "Character name", ref entry.CharaName, 30);
                }, () =>
                {
                    ImGuiEx.Text("@");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    WorldSelector.Instance.Draw(ref entry.CharaHomeWorld);
                });
                ImGui.TableNextColumn();

                ImGui.SetNextItemWidth(100);
                if(ImGui.BeginCombo("##from", $"{entry.BannedFrom.Count} worlds", ImGuiComboFlags.HeightLarge))
                {
                    Utils.DrawWorldSelector(entry.BannedFrom);
                    ImGui.EndCombo();
                }
                ImGui.TableNextColumn();

                ImGui.SetNextItemWidth(100);
                if(ImGui.BeginCombo("##to", $"{entry.BannedTo.Count} worlds", ImGuiComboFlags.HeightLarge))
                {
                    Utils.DrawWorldSelector(entry.BannedTo);
                    ImGui.EndCombo();
                }
                ImGui.TableNextColumn();

                if(ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                {
                    new TickScheduler(() => P.Config.TravelBans.Remove(entry));
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }
}
