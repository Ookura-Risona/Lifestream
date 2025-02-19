using ECommons.Automation.NeoTaskManager.Tasks;
using Lifestream.Enums;
using Lifestream.Schedulers;
using Lifestream.Tasks.SameWorld;

namespace Lifestream.Tasks.CrossWorld;

internal static class TaskTPAndChangeWorld
{
    internal static void Enqueue(string world, WorldChangeAetheryte gateway, bool insert)
    {
        P.TaskManager.BeginStack();
        if(P.Config.WaitForScreenReady) P.TaskManager.Enqueue(Utils.WaitForScreen);
        if (P.Config.LeavePartyBeforeWorldChange)
        {
            if (Svc.Condition[ConditionFlag.RecruitingWorldOnly])
            {
                P.TaskManager.Enqueue(WorldChange.ClosePF);
                P.TaskManager.Enqueue(WorldChange.OpenSelfPF);
                P.TaskManager.Enqueue(WorldChange.EndPF);
                P.TaskManager.Enqueue(WorldChange.WaitUntilNotRecruiting);
            }
            P.TaskManager.Enqueue(WorldChange.LeaveParty);
        }
        if(P.ActiveAetheryte != null && P.ActiveAetheryte.Value.IsWorldChangeAetheryte())
        {
            TaskChangeWorld.Enqueue(world, true);
        }
        else
        {
            if(Utils.GetReachableWorldChangeAetheryte(!P.Config.WalkToAetheryte) == null)
            {
                TaskTpToAethernetDestination.Enqueue(gateway);
            }
            P.TaskManager.EnqueueTask(new(() =>
            {
                if((P.ActiveAetheryte == null || !P.ActiveAetheryte.Value.IsWorldChangeAetheryte()) && Utils.GetReachableWorldChangeAetheryte() != null)
                {
                    P.TaskManager.InsertMulti(
                        new FrameDelayTask(10),
                        new(WorldChange.TargetReachableWorldChangeAetheryte),
                        new(WorldChange.LockOn),
                        new(WorldChange.EnableAutomove),
                        new(WorldChange.WaitUntilMasterAetheryteExists),
                        new(WorldChange.DisableAutomove)
                        );
                }
            }, "ConditionalLockonTask"));
            P.TaskManager.Enqueue(WorldChange.WaitUntilMasterAetheryteExists);
            P.TaskManager.EnqueueDelay(10, true);
            TaskChangeWorld.Enqueue(world, true);
        }
        if(insert)
        {
            P.TaskManager.InsertStack();
        }
        else
        {
            P.TaskManager.EnqueueStack();
        }
    }
}
