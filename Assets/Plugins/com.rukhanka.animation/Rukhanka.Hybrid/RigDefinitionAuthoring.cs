
using UnityEngine;

////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
[HelpURL("http://docs.rukhanka.com")]
[RequireComponent(typeof(Animator))]
public class RigDefinitionAuthoring: MonoBehaviour
{
    public enum BoneEntityStrippingMode
    {
        None,
        Automatic,
        Manual
    }

    [Tooltip("<color=Cyan><b>None</b></color> - keep all skeleton bone entities.\n<color=Cyan><b>Automatic</b></color> - automatically strip unreferenced bone entities.\n<color=Cyan><b>Manual</b></color> - included and stripped bone entities will be taken from specified avatar mask. This mode will make 'flat' bone hierarchy.")]
    public BoneEntityStrippingMode boneEntityStrippingMode;
    public AvatarMask boneStrippingMask;
    public bool hasAnimationEvents;
    public bool hasAnimatorControllerEvents;
}
}
