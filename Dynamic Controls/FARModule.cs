using System;
using System.Reflection;

namespace Dynamic_Controls
{
    public class FARModule : IModuleInterface
    {
        public FARModule(Part p)
        {
            controlSurface = p.Modules["FARControllableSurface"];
            farValToSet = controlSurface.GetType().GetField("maxdeflect");
        }

        public float GetMaxDeflect()
        {
            return (float)farValToSet.GetValue(controlSurface);
        }

        public void SetMaxDeflect(float val)
        {
            farValToSet.SetValue(controlSurface, val);
        }

        public float GetDefaultMaxDeflect()
        {
            return (float)farValToSet.GetValue(controlSurface.part.partInfo.partPrefab.Modules["FARControllableSurface"]);
        }

        private PartModule controlSurface;
        private FieldInfo farValToSet; // static?
    }
}
