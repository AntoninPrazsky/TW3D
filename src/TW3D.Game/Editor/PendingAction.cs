using System;

namespace TW3D.Editor
{
    /// <summary>How an attempt to save the level ended.</summary>
    enum SaveOutcome
    {
        /// <summary>The level was written.</summary>
        Saved,
        /// <summary>The level could not be written (the reason is in the status bar); it still has its changes.</summary>
        Failed,
        /// <summary>The level needs a name first: the Save as dialog is open, and it tells later how it ended.</summary>
        SavingAs
    }

    /// <summary>
    /// An action that would drop the unsaved changes of the level (a new level, opening another one, leaving the editor,
    /// quitting the game), kept while the editor asks "Save changes?". It runs at once when there are no changes; with
    /// changes it runs after Don't save, or after Save once the level was saved (through Save as when the level has no
    /// file yet), and it is dropped by Cancel, by a save that fails or by a Save as given up. At most one action waits:
    /// a new request replaces it.
    /// </summary>
    sealed class PendingAction
    {
        private Action action;
        //Save was the answer and the Save as dialog decides: the action runs when it saves
        private bool savingAs;

        /// <summary>The question "Save changes?" is to be answered (<see cref="DontSave"/>, <see cref="Saved"/> or <see cref="Cancel"/>).</summary>
        public bool IsAsking { get; private set; }

        /// <summary>An action waits: for the answer or for Save as.</summary>
        public bool IsPending => action != null;

        /// <summary>Asks for an action: it runs now when the level has no unsaved changes, otherwise it waits for the answer.</summary>
        /// <param name="dirty">The level has unsaved changes.</param>
        public void Request(bool dirty, Action run)
        {
            savingAs = false;
            if (!dirty)
            {
                action = null;
                IsAsking = false;
                run();
                return;
            }
            action = run;
            IsAsking = true;
        }

        /// <summary>The answer is Don't save: the action runs, and the changes are dropped by it.</summary>
        public void DontSave()
        {
            if (!IsAsking)
                return;
            IsAsking = false;
            RunAction();
        }

        /// <summary>The answer is Save, and saving was tried: the action runs when the level was saved and waits while Save as decides.</summary>
        public void Saved(SaveOutcome outcome)
        {
            if (!IsAsking)
                return;
            IsAsking = false;
            switch (outcome)
            {
                case SaveOutcome.Saved:
                    RunAction();
                    break;
                case SaveOutcome.SavingAs:
                    savingAs = true;
                    break;
                default:
                    action = null;
                    break;
            }
        }

        /// <summary>The Save as dialog ended (whatever opened it): the waiting action runs when the level was saved, else it is dropped.</summary>
        public void SaveAsFinished(bool saved)
        {
            if (!savingAs)
                return;
            savingAs = false;
            if (saved)
                RunAction();
            else
                action = null;
        }

        /// <summary>The answer is Cancel, or the action is no longer wanted: it is dropped.</summary>
        public void Cancel()
        {
            action = null;
            IsAsking = false;
            savingAs = false;
        }

        private void RunAction()
        {
            Action run = action;
            action = null;
            run?.Invoke();
        }
    }
}
