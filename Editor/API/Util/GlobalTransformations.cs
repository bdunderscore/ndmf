using nadena.dev.ndmf.animator;

namespace nadena.dev.ndmf.util
{
    public static class GlobalTransformations
    {
        public static void RemoveEmptyLayers(this AnimatorServicesContext ctx)
        {
            foreach (var controller in ctx.ControllerContext.GetAllControllers())
            {
                RemoveEmptyLayers(controller);
            }
        }

        public static void RemoveEmptyLayers(VirtualAnimatorController vac)
        {
            var isFirst = true;
            vac.RemoveLayers(layer =>
            {
                if (isFirst)
                {
                    isFirst = false;
                    return false;
                }

                return LayerIsEmpty(layer);
            });
        }

        private static bool LayerIsEmpty(VirtualLayer arg)
        {
            return arg.SyncedLayerIndex < 0 && (arg.StateMachine == null ||
                                                (arg.StateMachine.States.Count == 0 &&
                                                 arg.StateMachine.StateMachines.Count == 0));
        }
    }
}