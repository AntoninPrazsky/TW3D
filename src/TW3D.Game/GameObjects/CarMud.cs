using System;

namespace TW3D.GameObjects
{
    /// <summary>
    /// How muddy the car is (#60), drawn by the shader of the mud (Effects/Mud.fxh): the wheels of each axle (with their
    /// holder) and the body collect mud while the wheels are on the earth of the off-road track, a little standing or
    /// rolling slowly, more the faster they roll, much more sliding or spinning, and a splash at each hard landing; the
    /// body gets a part of what the wheels get, and a transition block gives half as much. The wheels report to it as they
    /// throw the earth up (<see cref="DirtSpray"/>, which measures the sliding, the rolling and the landings). The mud stays
    /// for the rest of the run (nothing washes it off); a new run, and the garage, start clean. Each amount grows by a part
    /// of what is still clean, so it nears 1 but never gets past it. Pure arithmetic, tested without the physics.
    /// </summary>
    sealed class CarMud
    {
        //How fast a wheel on earth muddies its axle (a part of what is still clean, a real second): standing or rolling
        //slowly, rolling fast (all of it at the full roll) and sliding (all of it at the full slide)
        public const float StandRate = 0.01f;
        public const float RollRate = 0.04f;
        public const float SlideRate = 0.15f;
        //The part of what is still clean a wheel landing hard on earth muddies (all of it at the full landing)
        public const float LandShare = 0.12f;
        //The body gets this part of the mud of the wheels
        public const float BodyShare = 0.25f;

        /// <summary>The mud of the wheels of the front axle and of their holder, 0 clean to 1.</summary>
        public float Front { get; private set; }

        /// <summary>The mud of the wheels of the rear axle and of their holder, 0 clean to 1.</summary>
        public float Rear { get; private set; }

        /// <summary>The mud of the body, 0 clean to 1.</summary>
        public float Body { get; private set; }

        /// <summary>Sets every amount at once (the development key B).</summary>
        public void Set(float amount)
        {
            Front = Rear = Body = Math.Clamp(amount, 0f, 1f);
        }

        /// <summary>A wheel on the ground for a while muddies its axle and the body.</summary>
        /// <param name="front">The wheel is on the front axle.</param>
        /// <param name="earth">How much of the ground is earth: 1 the off-road style, a half a transition into it, 0 none.</param>
        /// <param name="roll">How fast the car rolls, 0 to 1 (the full roll).</param>
        /// <param name="slide">How fast the tyre slides over the ground, 0 to 1 (the full slide).</param>
        /// <param name="landing">How hard the wheel landed now, 0 to 1 (0 when it did not land now).</param>
        /// <param name="elapsed">Real seconds the wheel was on the ground.</param>
        public void Wheel(bool front, float earth, float roll, float slide, float landing, float elapsed)
        {
            float share = ((StandRate + RollRate * roll + SlideRate * slide) * elapsed + LandShare * landing) * earth;
            if (share <= 0f)
                return;
            if (front)
                Front = Muddier(Front, share);
            else
                Rear = Muddier(Rear, share);
            Body = Muddier(Body, share * BodyShare);
        }

        /// <summary>An amount with a part of what is still clean muddied (a part of 1 at most).</summary>
        public static float Muddier(float amount, float share) => amount + (1f - amount) * Math.Clamp(share, 0f, 1f);
    }
}
