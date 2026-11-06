using System;

namespace TW3D.GameObjects
{
    /// <summary>
    /// How muddy the car is (#60), drawn by the shader of the mud (Effects/Mud.fxh): the wheels of each axle, the treads of
    /// their tyres, the holder of the axle and the body collect mud while the wheels are on the earth of the off-road track,
    /// a little standing or rolling slowly, more the faster they roll, much more sliding or spinning, and a splash at each
    /// hard landing; the treads get it twice as fast, the body a part of it, and a transition block gives half as much.
    /// Off the earth the wheels wash clean as they roll on (the treads in several seconds, leaving tracks of mud behind,
    /// see <see cref="TyreMarks.Mud"/>; the rest of the wheels in about twenty), faster spinning or sliding; the body and
    /// the holders keep their mud for the rest of the run. The wheels
    /// report to it as they throw the earth up (<see cref="DirtSpray"/>, which measures the sliding, the rolling and the
    /// landings). A new run, and the garage, start clean. Each amount grows by a part of what is still clean and washes by a
    /// part of what is muddy, so it stays between 0 and 1.
    /// <para>
    /// The mud is wet when it is fresh and dries (<see cref="Dry"/>): each part has its wetness, 0 dry to 1 wet, which the
    /// fresh mud of the earth raises (driving on the earth keeps the mud wet, a hard landing wets it at once) and which
    /// falls by a part of it every second, so the mud is dark when the car comes off the earth and gets lighter, and dark
    /// again from the next earth. Pure arithmetic, tested without the physics.
    /// </para>
    /// </summary>
    sealed class CarMud
    {
        //How fast a wheel on earth muddies its axle (a part of what is still clean, a real second): standing or rolling
        //slowly, rolling fast (all of it at the full roll) and sliding (all of it at the full slide)
        public const float StandRate = 0.01f;
        public const float RollRate = 0.04f;
        public const float SlideRate = 0.15f;
        //The part of what still is clean a wheel landing hard on earth muddies (all of it at the full landing)
        public const float LandShare = 0.12f;
        //The treads get this many times the mud of the wheels, the body this part of it
        public const float TreadShare = 2f;
        public const float BodyShare = 0.25f;
        //How fast a wheel off the earth washes its axle clean (a part of the mud, a real second): rolling (all of it at the
        //full roll) and sliding (all of it at the full slide); the treads this many times as fast
        public const float WashRollRate = 0.05f;
        public const float WashSlideRate = 0.1f;
        public const float TreadWash = 1.5f;
        //How fast the fresh mud wets a part (this many times its share of new mud, a part of what is still dry) and how
        //fast the mud dries (a part of its wetness, a real second)
        public const float WetRate = 20f;
        public const float DryRate = 0.05f;

        //The front axle and the rear one: the amounts, and the wetness of the wheels (their treads with them) and of the
        //holders
        private readonly float[] wheels = new float[2], treads = new float[2], holders = new float[2];
        private readonly float[] wheelsWet = new float[2], holdersWet = new float[2];

        /// <summary>The mud of the wheels of an axle (but the treads of their tyres), 0 clean to 1.</summary>
        public float Wheels(bool front) => wheels[Axle(front)];

        /// <summary>The mud of the treads of the tyres of an axle, 0 clean to 1.</summary>
        public float Treads(bool front) => treads[Axle(front)];

        /// <summary>The mud of the holder of an axle, 0 clean to 1.</summary>
        public float Holder(bool front) => holders[Axle(front)];

        /// <summary>The mud of the body, 0 clean to 1.</summary>
        public float Body { get; private set; }

        /// <summary>How wet the mud of the wheels of an axle (and of their treads) is, 0 dry to 1 fresh.</summary>
        public float WheelsWet(bool front) => wheelsWet[Axle(front)];

        /// <summary>How wet the mud of the holder of an axle is, 0 dry to 1 fresh.</summary>
        public float HolderWet(bool front) => holdersWet[Axle(front)];

        /// <summary>How wet the mud of the body is, 0 dry to 1 fresh.</summary>
        public float BodyWet { get; private set; }

        /// <summary>Sets every amount at once, the mud fresh (the development key B).</summary>
        public void Set(float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            for (int axle = 0; axle < 2; axle++)
            {
                wheels[axle] = treads[axle] = holders[axle] = amount;
                wheelsWet[axle] = holdersWet[axle] = 1f;
            }
            Body = amount;
            BodyWet = 1f;
        }

        /// <summary>Dries the mud for a while.</summary>
        /// <param name="elapsed">Real seconds.</param>
        public void Dry(float elapsed)
        {
            //What is left of the wetness, the same in one long step as in many short ones
            float left = MathF.Exp(-DryRate * elapsed);
            for (int axle = 0; axle < 2; axle++)
            {
                wheelsWet[axle] *= left;
                holdersWet[axle] *= left;
            }
            BodyWet *= left;
        }

        /// <summary>A wheel on the ground for a while muddies its axle and the body, or off the earth washes its axle clean.</summary>
        /// <param name="front">The wheel is on the front axle.</param>
        /// <param name="earth">How much of the ground is earth: 1 the off-road style, a half a transition into it, 0 none.</param>
        /// <param name="roll">How fast the car rolls, 0 to 1 (the full roll).</param>
        /// <param name="slide">How fast the tyre slides over the ground, 0 to 1 (the full slide).</param>
        /// <param name="landing">How hard the wheel landed now, 0 to 1 (0 when it did not land now).</param>
        /// <param name="elapsed">Real seconds the wheel was on the ground.</param>
        public void Wheel(bool front, float earth, float roll, float slide, float landing, float elapsed)
        {
            int axle = Axle(front);
            if (earth <= 0f)
            {
                float wash = (WashRollRate * roll + WashSlideRate * slide) * elapsed;
                wheels[axle] = Washed(wheels[axle], wash);
                treads[axle] = Washed(treads[axle], wash * TreadWash);
                return;
            }
            float share = ((StandRate + RollRate * roll + SlideRate * slide) * elapsed + LandShare * landing) * earth;
            wheels[axle] = Muddier(wheels[axle], share);
            treads[axle] = Muddier(treads[axle], share * TreadShare);
            holders[axle] = Muddier(holders[axle], share);
            Body = Muddier(Body, share * BodyShare);
            wheelsWet[axle] = Muddier(wheelsWet[axle], share * WetRate);
            holdersWet[axle] = Muddier(holdersWet[axle], share * WetRate);
            BodyWet = Muddier(BodyWet, share * BodyShare * WetRate);
        }

        /// <summary>An amount with a part of what is still clean muddied (a part of 1 at most).</summary>
        public static float Muddier(float amount, float share) => amount + (1f - amount) * Math.Clamp(share, 0f, 1f);

        /// <summary>An amount with a part of its mud washed off (a part of 1 at most).</summary>
        public static float Washed(float amount, float share) => amount * (1f - Math.Clamp(share, 0f, 1f));

        private static int Axle(bool front) => front ? 0 : 1;
    }
}
