namespace Dino_Engine.Modelling.Procedural
{
    /// <summary>
    /// When every material has its number (<see cref="Flora.FloraMaterials"/>, <see cref="Gear.GearMaterials"/>, <see cref="Fence.FenceMaterials"/>, <see cref="Rock.RockMaterials"/>), which is all
    /// a generator needs to make a model on another thread. The numbers are given out as the textures are made, and the rest of the engine is made after, so a model can be begun
    /// before there's a whole engine. Nothing here touches a GPU, so it can be waited on before there's a window.
    /// </summary>
    public static class MaterialNumbers
    {
        private static readonly TaskCompletionSource Given = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static Task Ready => Given.Task;

        /// <summary>Said by whatever gives the numbers out, once it has.</summary>
        public static void Settle() => Given.TrySetResult();
    }
}
