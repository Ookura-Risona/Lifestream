using ECommons.ExcelServices;
using ECommons.GameHelpers;
using Lifestream.Services;
using Lifestream.Tasks.SameWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lifestream.Data;
[Serializable]
public class CustomAliasCommand
{
    internal string ID = Guid.NewGuid().ToString();
    public CustomAliasKind Kind;
    public Vector3 Point;
    public uint Aetheryte;
    public int World;

    public void Enqueue()
    {
        if(this.Kind == CustomAliasKind.跨服)
        {
            if(Player.Object.HomeWorld.Id != Player.Object.CurrentWorld.Id)
            {
                var world = ExcelWorldHelper.GetName(World);
                if(P.IPCProvider.CanVisitCrossDC(world))
                {
                    P.TPAndChangeWorld(world, true);
                }
                else if(P.IPCProvider.CanVisitSameDC(world))
                {
                    P.TPAndChangeWorld(world, false);
                }
            }
        }
        else if(this.Kind == CustomAliasKind.步行到坐标)
        {
            P.TaskManager.Enqueue(() => P.FollowPath.Move([this.Point], true));
            P.TaskManager.Enqueue(() => P.FollowPath.Waypoints.Count > 0);
        }
        else if(this.Kind == CustomAliasKind.寻路到坐标)
        {
            P.TaskManager.Enqueue(() =>
            {
                var task = P.VnavmeshManager.Pathfind(Player.Position, this.Point, false);
                P.TaskManager.InsertMulti(
                    new(() => task.IsCompleted),
                    new(() => P.FollowPath.Move(task.Result, true)),
                    new(() => P.FollowPath.Waypoints.Count > 0)
                    );
            });
        }
        else if(this.Kind == CustomAliasKind.传送到以太之光)
        {
            P.TaskManager.Enqueue((Action)(() => S.TeleportService.TeleportToAetheryte(this.Aetheryte)));
            P.TaskManager.Enqueue(() => !IsScreenReady());
            P.TaskManager.Enqueue(() => IsScreenReady());
        }
    }
}
