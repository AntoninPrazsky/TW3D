using TW3D.Editor;
using Xunit;

namespace TW3D.Tests
{
    /// <summary>
    /// The action of the editor that waits for "Save changes?" (Editor/PendingAction): every path from the request to the
    /// action running or being dropped.
    /// </summary>
    public class PendingActionTests
    {
        private int runs;

        private void Run() => runs++;

        [Fact]
        public void WithoutChangesTheActionRunsAtOnce()
        {
            PendingAction pending = new PendingAction();
            pending.Request(false, Run);
            Assert.Equal(1, runs);
            Assert.False(pending.IsAsking);
            Assert.False(pending.IsPending);
        }

        [Fact]
        public void WithChangesItAsksFirst()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            Assert.Equal(0, runs);
            Assert.True(pending.IsAsking);
            Assert.True(pending.IsPending);
        }

        [Fact]
        public void DontSaveRunsIt()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            pending.DontSave();
            Assert.Equal(1, runs);
            Assert.False(pending.IsPending);
            //Once only
            pending.DontSave();
            Assert.Equal(1, runs);
        }

        [Fact]
        public void CancelDropsIt()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            pending.Cancel();
            Assert.False(pending.IsAsking);
            Assert.False(pending.IsPending);
            pending.DontSave();
            pending.Saved(SaveOutcome.Saved);
            pending.SaveAsFinished(true);
            Assert.Equal(0, runs);
        }

        [Fact]
        public void ASaveRunsIt()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            pending.Saved(SaveOutcome.Saved);
            Assert.Equal(1, runs);
            Assert.False(pending.IsPending);
        }

        [Fact]
        public void AFailedSaveDropsIt()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            pending.Saved(SaveOutcome.Failed);
            Assert.False(pending.IsPending);
            pending.SaveAsFinished(true);
            Assert.Equal(0, runs);
        }

        [Fact]
        public void ItWaitsForSaveAsAndRunsWhenItSaved()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            pending.Saved(SaveOutcome.SavingAs);
            Assert.False(pending.IsAsking);
            Assert.True(pending.IsPending);
            Assert.Equal(0, runs);
            pending.SaveAsFinished(true);
            Assert.Equal(1, runs);
            Assert.False(pending.IsPending);
        }

        [Fact]
        public void SaveAsGivenUpDropsIt()
        {
            PendingAction pending = new PendingAction();
            pending.Request(true, Run);
            pending.Saved(SaveOutcome.SavingAs);
            pending.SaveAsFinished(false);
            Assert.False(pending.IsPending);
            pending.SaveAsFinished(true);
            Assert.Equal(0, runs);
        }

        [Fact]
        public void ASaveAsOfItsOwnRunsNothing()
        {
            PendingAction pending = new PendingAction();
            //Save as opened by Ctrl+Shift+S while the question is asked does not answer it
            pending.Request(true, Run);
            pending.SaveAsFinished(true);
            Assert.True(pending.IsAsking);
            Assert.Equal(0, runs);
        }

        [Fact]
        public void ANewRequestReplacesTheWaitingOne()
        {
            PendingAction pending = new PendingAction();
            int other = 0;
            pending.Request(true, Run);
            pending.Saved(SaveOutcome.SavingAs);
            pending.Request(true, () => other++);
            //The Save as of the first request no longer counts
            pending.SaveAsFinished(true);
            Assert.Equal(0, runs);
            Assert.Equal(0, other);
            pending.DontSave();
            Assert.Equal(0, runs);
            Assert.Equal(1, other);
        }
    }
}
