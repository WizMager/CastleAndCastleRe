using Unity.Entities;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public static class IKCommon
{
    public static void GetEntityWorldTransform(Entity e, ref BoneTransform t, ComponentLookup<LocalTransform> ltl, ComponentLookup<Parent> pl)
    {
        if (!ltl.TryGetComponent(e, out var lt)) return;

        t = BoneTransform.Multiply(new BoneTransform(lt), t);

        if (pl.TryGetComponent(e, out var p))
        {
            GetEntityWorldTransform(p.Value, ref t, ltl, pl);
        }
    }
    
}
}
