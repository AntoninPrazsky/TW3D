using System;
using System.Collections.Generic;
using TW3D.Input;

namespace TW3D.GamePlay
{
    /// <summary>What the player does with the car in a frame, as far as the tutorial asks for it.</summary>
    public readonly struct DriveActions
    {
        public DriveActions(bool throttle, bool axles, bool brake, bool lockAxles)
        {
            Throttle = throttle;
            Axles = axles;
            Brake = brake;
            LockAxles = lockAxles;
        }

        /// <summary>Driving forward (not reversing).</summary>
        public bool Throttle { get; }

        /// <summary>Turning the axles, either way.</summary>
        public bool Axles { get; }

        public bool Brake { get; }

        public bool LockAxles { get; }
    }

    /// <summary>One step of a lesson: what its card says, and what the player has to hold, and how long, to do it.</summary>
    public sealed class LessonStep
    {
        public LessonStep(CarControl control, string caption, string detail, Func<DriveActions, bool> held, float holdTime, bool continuous)
        {
            Control = control;
            Caption = caption;
            Detail = detail;
            Held = held;
            HoldTime = holdTime;
            Continuous = continuous;
        }

        public CarControl Control { get; }

        /// <summary>The instruction: one imperative, the glyph of the control beside it.</summary>
        public string Caption { get; }

        /// <summary>A short line under the instruction (how), or null.</summary>
        public string Detail { get; }

        public Func<DriveActions, bool> Held { get; }

        /// <summary>Seconds of holding that do the step.</summary>
        public float HoldTime { get; }

        /// <summary>The hold has to be unbroken; otherwise the seconds add up.</summary>
        public bool Continuous { get; }
    }

    /// <summary>
    /// A lesson of the tutorial: an action (one or two steps, done when the player does them), a notice to read, or a
    /// hint (an action shown when the player needs it, never saved).
    /// </summary>
    public sealed class Lesson
    {
        private Lesson(string key, int fromLevel, string caption, string praise, LessonStep[] steps, bool isHint = false)
        {
            Key = key;
            FromLevel = fromLevel;
            Caption = caption;
            Praise = praise;
            Steps = steps;
            IsHint = isHint;
        }

        /// <summary>An action of one or two steps; the praise joins the card when it is done.</summary>
        public static Lesson Action(string key, int fromLevel, string praise, params LessonStep[] steps)
        {
            return new Lesson(key, fromLevel, null, praise, steps);
        }

        /// <summary>A hint: an action of one step, shown whenever it is due, on any level.</summary>
        public static Lesson Hint(string key, string praise, LessonStep step)
        {
            return new Lesson(key, 0, null, praise, new[] { step }, true);
        }

        /// <summary>A notice, read in a few seconds.</summary>
        public static Lesson Notice(string key, int fromLevel, string caption)
        {
            return new Lesson(key, fromLevel, caption, null, Array.Empty<LessonStep>());
        }

        /// <summary>The name it is saved under once taught; it must not change, or every player is taught it again.</summary>
        public string Key { get; }

        /// <summary>The first level it is taught in (the position in the level select, 0 = the first level).</summary>
        public int FromLevel { get; }

        /// <summary>The text of a notice; null for an action.</summary>
        public string Caption { get; }

        public string Praise { get; }

        public IReadOnlyList<LessonStep> Steps { get; }

        public bool IsNotice => Steps.Count == 0;

        /// <summary>Shown when it is due, not taught once (see <see cref="Tutorial.StuckHint"/>).</summary>
        public bool IsHint { get; }
    }

    /// <summary>The tutorial's levers for development (see Program).</summary>
    public enum TutorialLever
    {
        None,
        /// <summary>Every lesson of the level and before it, taught or not, saving nothing.</summary>
        Replay,
        /// <summary>Every lesson in turn on any level, each done by itself after a while (for screenshots), saving nothing.</summary>
        Demo
    }

    /// <summary>
    /// The tutorial of a run of a level: cards over the level that teach the controls one at a time, after the
    /// tutorial of BS3D. A lesson is taught from its level on; one not taught yet follows the player into the next
    /// levels. An action stays until the player does it (then a word of praise joins it) and gives up after a while
    /// unrecorded, to come again in the next run; a notice stays a few seconds. A lesson taught is saved and not shown
    /// again. A hint comes whenever the player needs it (see <see cref="StuckHint"/>). The cards never stop the game.
    /// This class holds the lessons and their state and draws nothing (see <see cref="TutorialCard"/>).
    /// </summary>
    public sealed class Tutorial
    {
        /// <summary>Seconds the card takes to arrive, and to leave.</summary>
        public const float ArriveTime = 0.4f;
        public const float LeaveTime = 0.35f;

        //After the start, and between two cards (seconds)
        private const float FirstCardDelay = 1f;
        private const float CardGap = 0.6f;
        //An instruction stands at least this long, whatever the player does; the praise this long after the action
        private const float MinReadTime = 1.2f;
        private const float PraiseTime = 1.1f;
        //An action not done goes after this long, unrecorded; a notice stays this long
        private const float ActionTimeout = 22f;
        private const float NoticeTime = 7f;
        //The second step of an action goes back to the first when the first one's hold is let go this long
        private const float StepBackTime = 0.5f;
        //In the demo, every step is done after this long
        private const float DemoStepTime = 2f;
        //The stuck hint comes when the throttle has been held this long while the car made no progress (went slower
        //than this, physics units a second); unheeded, it goes after this long
        private const float StuckTime = 3f;
        private const float StuckSpeed = 3f;
        private const float HintTime = 7f;

        /// <summary>The lessons, in the order they are taught.</summary>
        public static readonly IReadOnlyList<Lesson> Lessons = new[]
        {
            Lesson.Action("drive", 0, "Nice!",
                new LessonStep(CarControl.Throttle, "Hold to drive", null, a => a.Throttle, 1f, false)),
            Lesson.Action("axles", 0, "Good!",
                new LessonStep(CarControl.Axles, "Hold to turn the axles", null, a => a.Axles, 0.6f, false)),
            Lesson.Notice("par", 1, "Beat the par time for three stars"),
            Lesson.Action("walk", 1, "Great!",
                new LessonStep(CarControl.Brake, "Hold to brake", null, a => a.Brake, 0.35f, true),
                new LessonStep(CarControl.Axles, "Now turn the axles", "Keep braking", a => a.Brake && a.Axles, 0.35f, true)),
            Lesson.Action("lock", 2, "Perfect!",
                new LessonStep(CarControl.Lock, "Hold to lock the axles", null, a => a.LockAxles, 0.35f, true)),
        };

        /// <summary>
        /// The hint for a car stuck in front of an obstacle with the throttle held, after BS3D's precise-aim hint: it
        /// counts only the time the throttle is held while the car makes no progress, so a player who stops to think is
        /// not nagged, and turning the axles starts the count again.
        /// </summary>
        public static readonly Lesson StuckHint = Lesson.Hint("stuck", "That's it!",
            new LessonStep(CarControl.Axles, "Turn the axles to climb over", null, a => a.Axles, 0.6f, false));

        /// <summary>Set once at the start by the command line (see Program).</summary>
        public static TutorialLever Lever { get; set; }

        private readonly Queue<Lesson> queue = new Queue<Lesson>();
        private readonly Action<string> taught;

        //Seconds until the next card, while there is none
        private float wait = FirstCardDelay;
        //Seconds the steps' actions have been held; the first step's hold let go (second step)
        private float held;
        private float released;
        private bool leaving;
        //Seconds the throttle has been held while the car made no progress
        private float stuck;

        /// <param name="level">The position of the level in the level select (0 = the first one).</param>
        /// <param name="taughtLessons">The keys of the lessons taught before.</param>
        /// <param name="taught">Saves a lesson taught now, by its key.</param>
        public Tutorial(int level, ICollection<string> taughtLessons, Action<string> taught)
        {
            this.taught = taught;
            foreach (Lesson lesson in Lessons)
            {
                bool due = Lever switch
                {
                    TutorialLever.Demo => true,
                    TutorialLever.Replay => lesson.FromLevel <= level,
                    _ => lesson.FromLevel <= level && !taughtLessons.Contains(lesson.Key)
                };
                if (due)
                    queue.Enqueue(lesson);
            }
            if (Lever == TutorialLever.Demo)
                queue.Enqueue(StuckHint);
        }

        /// <summary>The lesson of the card shown; null when there is none.</summary>
        public Lesson Lesson { get; private set; }

        /// <summary>The step of an action shown (0 or 1).</summary>
        public int Step { get; private set; }

        public LessonStep CurrentStep => Lesson != null && !Lesson.IsNotice ? Lesson.Steps[Step] : null;

        /// <summary>Seconds since the card arrived, and since its step began.</summary>
        public float Age { get; private set; }
        public float StepAge { get; private set; }

        /// <summary>Seconds since the action was done (the praise shows); null while it is not.</summary>
        public float? PraiseAge { get; private set; }

        /// <summary>0 while the card is up, rising to 1 as it leaves.</summary>
        public float Leaving { get; private set; }

        /// <summary>Moves the tutorial on.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="racing">The run is on: before the start no card comes, at the finish the card goes at once.</param>
        /// <param name="carSpeed">The speed of the car forward (physics units a second).</param>
        public void Update(float elapsed, bool racing, DriveActions actions, float carSpeed)
        {
            if (!racing)
            {
                Lesson = null;
                stuck = 0f;
                return;
            }

            if (actions.Axles || carSpeed >= StuckSpeed)
                stuck = 0f;
            else if (actions.Throttle)
                stuck += elapsed;

            if (Lesson == null)
            {
                wait -= elapsed;
                //A hint due comes first, as soon as no card is up
                if (stuck >= StuckTime)
                {
                    stuck = 0f;
                    Show(StuckHint);
                }
                else if (wait <= 0f && queue.Count > 0)
                    Show(queue.Dequeue());
                return;
            }

            Age += elapsed;
            StepAge += elapsed;
            if (PraiseAge.HasValue)
                PraiseAge += elapsed;

            if (leaving)
            {
                Leaving += elapsed / LeaveTime;
                if (Leaving >= 1f)
                {
                    //A hint counts again from when it is gone, so a player still stuck has a while without it
                    if (Lesson.IsHint)
                        stuck = 0f;
                    Lesson = null;
                    wait = CardGap;
                }
                return;
            }

            if (Lesson.IsNotice)
            {
                if (Age >= NoticeTime)
                {
                    Teach();
                    Leave();
                }
                return;
            }

            if (PraiseAge.HasValue)
            {
                //The instruction stands its time, the praise its own; the card goes when both have run
                if (Age >= MinReadTime && PraiseAge >= PraiseTime)
                    Leave();
                return;
            }

            Practise(elapsed, actions);
            if (!PraiseAge.HasValue && Age >= (Lesson.IsHint ? HintTime : ActionTimeout))
                Leave();
        }

        private void Show(Lesson lesson)
        {
            Lesson = lesson;
            Step = 0;
            Age = 0f;
            StepAge = 0f;
            PraiseAge = null;
            Leaving = 0f;
            leaving = false;
            held = 0f;
            released = 0f;
        }

        /// <summary>Follows what the player does for the step shown.</summary>
        private void Practise(float elapsed, DriveActions actions)
        {
            LessonStep step = CurrentStep;
            bool holding = Lever == TutorialLever.Demo ? StepAge >= DemoStepTime : step.Held(actions);
            if (holding)
                held += elapsed;
            else if (step.Continuous)
                held = 0f;

            //Letting the first step go takes the second one back to it
            if (Step > 0 && Lever != TutorialLever.Demo)
            {
                released = Lesson.Steps[0].Held(actions) ? 0f : released + elapsed;
                if (released >= StepBackTime)
                {
                    Step = 0;
                    StepAge = 0f;
                    held = 0f;
                    released = 0f;
                    return;
                }
            }

            if (held < step.HoldTime)
                return;

            if (Step + 1 < Lesson.Steps.Count)
            {
                //A step already held when it came stands its time before the card turns
                if (StepAge >= MinReadTime)
                {
                    Step++;
                    StepAge = 0f;
                    held = 0f;
                    released = 0f;
                }
            }
            else
            {
                PraiseAge = 0f;
                Teach();
            }
        }

        private void Teach()
        {
            if (Lever == TutorialLever.None && !Lesson.IsHint)
                taught(Lesson.Key);
        }

        private void Leave()
        {
            leaving = true;
        }
    }
}
