using System;

namespace Dynamic_Controls
{
    public class StockModule : IModuleInterface
    {
        public StockModule(ModuleControlSurface module)
        {
            controlSurface = module;
        }

        public float GetMaxDeflect()
        {
            return controlSurface.ctrlSurfaceRange;
        }

        public void SetMaxDeflect(float val)
        {
            controlSurface.ctrlSurfaceRange = val;
        }

        public float GetDefaultMaxDeflect()
        {
            return controlSurface.part.partInfo.partPrefab.Modules.GetModule<ModuleControlSurface>().ctrlSurfaceRange;
        }

        private ModuleControlSurface controlSurface;
    }
}
