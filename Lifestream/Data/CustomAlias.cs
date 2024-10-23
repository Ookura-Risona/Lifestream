using NightmareUI.OtterGuiWrapper.FileSystems.Generic;

namespace Lifestream.Data;
[Serializable]
public class CustomAlias : IFileSystemStorage
{
    public string ExportedName;
    public Guid GUID { get; set; } = Guid.NewGuid();
    public string Alias = "";
    public bool Enabled = true;
    public List<CustomAliasCommand> Commands = [];

    public string GetCustomName() => null;
    public void SetCustomName(string s) { }

    public void Enqueue(bool force = false)
    {
        if(force || !Utils.IsBusy())
        {
            for(var i = 0; i < Commands.Count; i++)
            {
                List<Vector3> append = [];
                var cmd = Commands[i];
                if(cmd.Kind.EqualsAny(CustomAliasKind.步行到坐标, CustomAliasKind.寻路到坐标, CustomAliasKind.Circular_movement) == true)
                {
                    while(Commands.SafeSelect(i + 1)?.Kind == CustomAliasKind.步行到坐标)
                    {
                        append.Add(Commands[i + 1].Point);
                        i++;
                    }
                }
                cmd.Enqueue(append);
            }
        }
        else
        {
            Notify.Error("Lifestream is busy!");
        }
    }
}
