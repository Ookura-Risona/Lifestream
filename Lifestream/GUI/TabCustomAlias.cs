using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lifestream.Data;
using Lumina.Excel.GeneratedSheets;
using NightmareUI;
using NightmareUI.ImGuiElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aetheryte = Lumina.Excel.GeneratedSheets.Aetheryte;

namespace Lifestream.GUI;
public static class TabCustomAlias
{
    public static void Draw()
    {
        ImGuiEx.Text(EColor.RedBright, "Alpha 功能，请报告错误。");
        var selector = S.CustomAliasFileSystemManager.FileSystem.Selector;
        selector.Draw(150f);
        ImGui.SameLine();
        if(ImGui.BeginChild("Child"))
        {
            if(selector.Selected != null)
            {
                var item = selector.Selected;
                DrawAlias(item);
            }
            else
            {
                ImGuiEx.TextWrapped($"首先，选择要编辑的别名或创建新别名。");
            }
        }
        ImGui.EndChild();
    }

    private static void DrawAlias(CustomAlias selected)
    {
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Plus, "新增"))
        {
            selected.Commands.Add(new());
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f);
        ImGui.InputText($"别名", ref selected.Alias, 50);
        ImGuiEx.HelpMarker($"将通过 \"/li {selected.Alias}\" 命令可用");
        for(var i = 0; i < selected.Commands.Count; i++)
        {
            var x = selected.Commands[i];
            ImGui.PushID(x.ID);
            if(ImGui.ArrowButton("##up", ImGuiDir.Up))
            {
                if(i > 0) (selected.Commands[i], selected.Commands[i - 1]) = (selected.Commands[i - 1], selected.Commands[i]);
            }
            ImGui.SameLine(0, 1);
            if(ImGui.ArrowButton("##down", ImGuiDir.Down))
            {
                if(i < selected.Commands.Count - 1) (selected.Commands[i], selected.Commands[i + 1]) = (selected.Commands[i + 1], selected.Commands[i]);
            }
            ImGui.SameLine(0, 1);
            ImGui.PopID();
            ImGuiEx.TreeNodeCollapsingHeader($"命令 {i + 1}: {x.Kind}###{x.ID}", () => DrawCommand(x, selected));
        }
    }

    private static void DrawCommand(CustomAliasCommand command, CustomAlias selected)
    {
        ImGui.PushID(command.ID);
        var aetherytes = Ref<uint[]>.Get("Aetherytes", () => Svc.Data.GetExcelSheet<Aetheryte>().Where(x => x.PlaceName.Value?.Name?.ToString().IsNullOrEmpty() == false && x.IsAetheryte).Select(x => x.RowId).ToArray());
        var names = Ref<Dictionary<uint, string>>.Get("Aetherytes", () => aetherytes.Select(Svc.Data.GetExcelSheet<Aetheryte>().GetRow).ToDictionary(x => x.RowId, x => x.PlaceName.Value.Name.ToString()));
        ImGui.Separator();
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.EnumCombo("别名种类", ref command.Kind);
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Trash, "删除"))
        {
            new TickScheduler(() => selected.Commands.Remove(command));
        }

        if(command.Kind == CustomAliasKind.传送到以太之光)
        {
            ImGui.SetNextItemWidth(150f);
            ImGuiEx.Combo("选择要传送的以太之光", ref command.Aetheryte, aetherytes, names: names);
        }

        if(command.Kind.EqualsAny(CustomAliasKind.步行到坐标, CustomAliasKind.寻路到坐标))
        {
            ImGui.SetNextItemWidth(200f);
            ImGui.InputFloat3("坐标", ref command.Point);
            ImGui.SameLine();
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.User, "我的位置", Player.Available))
            {
                command.Point = Player.Position;
            }
        }

        if(command.Kind == CustomAliasKind.跨服)
        {
            ImGui.SetNextItemWidth(150f);
            Ref<WorldSelector>.Get("Selector", () => new()).Draw(ref command.World);
            ImGui.SameLine();
            ImGuiEx.Text("选择服务器");
        }
        ImGui.PopID();
    }
}
