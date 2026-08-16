using System;

namespace AdjustNamespace.Adjusting.Plan
{
    /// <summary>
    /// The outcome of <see cref="AdjustPlanner"/> for one file: adjust it, block it
    /// with a reason the user has to see, or drop it silently (already correct).
    /// </summary>
    public readonly struct AdjustPlanResult
    {
        /// <summary>
        /// The file is going to be adjusted.
        /// </summary>
        public AdjustPlanItem? Plan
        {
            get;
        }

        /// <summary>
        /// The file cannot be adjusted; the reason is shown to the user.
        /// </summary>
        public AdjustBlock? Block
        {
            get;
        }

        /// <summary>
        /// There is a plan.
        /// </summary>
        public bool HasPlan => Plan.HasValue;

        /// <summary>
        /// The file is blocked.
        /// </summary>
        public bool HasBlock => Block.HasValue;

        /// <summary>
        /// Neither a plan nor a block: the file is already fine (or a xaml with nothing
        /// to change) and is dropped silently.
        /// </summary>
        public bool IsNone => !HasPlan && !HasBlock;

        private AdjustPlanResult(
            AdjustPlanItem? plan,
            AdjustBlock? block
            )
        {
            Plan = plan;
            Block = block;
        }

        public static AdjustPlanResult ForPlan(
            AdjustPlanItem plan
            )
        {
            return new AdjustPlanResult(plan, null);
        }

        public static AdjustPlanResult ForBlock(
            AdjustBlock block
            )
        {
            return new AdjustPlanResult(null, block);
        }

        public static AdjustPlanResult None()
        {
            return new AdjustPlanResult(null, null);
        }
    }
}
