using Rukhanka.Hybrid;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
    
////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Editor
{
[CustomEditor(typeof(RigDefinitionAuthoring))]
public class RigDefinitionAuthoringEditor : UnityEditor.Editor
{
    public VisualTreeAsset inpectorXML;
    VisualElement boneStrippingMaskElement;
    
////////////////////////////////////////////////////////////////////////////////////////

    public override VisualElement CreateInspectorGUI()
    {
        var myInspector = new VisualElement(); 
        inpectorXML.CloneTree(myInspector);
        boneStrippingMaskElement = myInspector.Q("boneStrippingMask");
        myInspector.TrackSerializedObjectValue(serializedObject, ShowOrHideBoneStrippingMask);
        ShowOrHideBoneStrippingMask(serializedObject);
        return myInspector;
    }
    
////////////////////////////////////////////////////////////////////////////////////////

    void ShowOrHideBoneStrippingMask(SerializedObject so)
    {
        //  Hide bone stripping mask for "None" and "Automatic" modes
        var strippingMode = serializedObject.FindProperty("boneEntityStrippingMode");
        var showBoneStrippingMask = (RigDefinitionAuthoring.BoneEntityStrippingMode)strippingMode.enumValueIndex == RigDefinitionAuthoring.BoneEntityStrippingMode.Manual;
        boneStrippingMaskElement.visible = showBoneStrippingMask;
    }
}
}
