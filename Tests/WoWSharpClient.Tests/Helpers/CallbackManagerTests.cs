using WoWSharpClient.Networking.ClientComponents.Helpers;

namespace WoWSharpClient.Tests.Helpers
{
    public class GenericCallbackManagerTests
    {
        [Fact]
        public void InvokeCallbacks_NoPermanent_NoTemporary_DoesNotThrow()
        {
            var mgr = new CallbackManager<int>();
            mgr.InvokeCallbacks(42); // No callbacks — should not throw
        }

        [Fact]
        public void SetPermanentCallback_InvokedOnInvoke()
        {
            var mgr = new CallbackManager<int>();
            int received = 0;
            mgr.SetPermanentCallback(v => received = v);

            mgr.InvokeCallbacks(99);

            Assert.Equal(99, received);
        }

        [Fact]
        public void SetPermanentCallback_Null_ClearsPrevious()
        {
            var mgr = new CallbackManager<int>();
            int callCount = 0;
            mgr.SetPermanentCallback(_ => callCount++);
            mgr.InvokeCallbacks(1);
            Assert.Equal(1, callCount);

            mgr.SetPermanentCallback(null);
            mgr.InvokeCallbacks(2);
            Assert.Equal(1, callCount); // Not called again
        }

        [Fact]
        public void AddTemporaryCallback_InvokedOnInvoke()
        {
            var mgr = new CallbackManager<string>();
            string received = "";
            mgr.AddTemporaryCallback(v => received = v);

            mgr.InvokeCallbacks("hello");

            Assert.Equal("hello", received);
        }

        [Fact]
        public void AddTemporaryCallback_DisposableRemovesCallback()
        {
            var mgr = new CallbackManager<int>();
            int callCount = 0;
            var sub = mgr.AddTemporaryCallback(_ => callCount++);

            mgr.InvokeCallbacks(1);
            Assert.Equal(1, callCount);

            sub.Dispose();
            mgr.InvokeCallbacks(2);
            Assert.Equal(1, callCount); // Not called after disposal
        }

        [Fact]
        public void Dispose_Idempotent()
        {
            var mgr = new CallbackManager<int>();
            int callCount = 0;
            var sub = mgr.AddTemporaryCallback(_ => callCount++);

            sub.Dispose();
            sub.Dispose(); // Should not throw or double-remove

            mgr.InvokeCallbacks(1);
            Assert.Equal(0, callCount);
        }

        [Fact]
        public void PermanentCallback_InvokedBeforeTemporary()
        {
            var mgr = new CallbackManager<int>();
            var order = new List<string>();

            mgr.SetPermanentCallback(_ => order.Add("permanent"));
            mgr.AddTemporaryCallback(_ => order.Add("temp1"));
            mgr.AddTemporaryCallback(_ => order.Add("temp2"));

            mgr.InvokeCallbacks(0);

            Assert.Equal(["permanent", "temp1", "temp2"], order);
        }

        [Fact]
        public void MultipleTemporaryCallbacks_AllInvoked()
        {
            var mgr = new CallbackManager<int>();
            int sum = 0;
            mgr.AddTemporaryCallback(v => sum += v);
            mgr.AddTemporaryCallback(v => sum += v * 10);

            mgr.InvokeCallbacks(5);

            Assert.Equal(55, sum); // 5 + 50
        }

        [Fact]
        public void TemporaryCallback_ExceptionSwallowed_OthersContinue()
        {
            var mgr = new CallbackManager<int>();
            int callCount = 0;
            mgr.AddTemporaryCallback(_ => throw new InvalidOperationException("boom"));
            mgr.AddTemporaryCallback(_ => callCount++);

            mgr.InvokeCallbacks(1); // Should not throw

            Assert.Equal(1, callCount); // Second callback still called
        }

        [Fact]
        public void PermanentCallback_ExceptionNotSwallowed()
        {
            var mgr = new CallbackManager<int>();
            mgr.SetPermanentCallback(_ => throw new InvalidOperationException("permanent boom"));

            // Permanent callback exception is NOT caught (only temporary callbacks are wrapped in try/catch)
            Assert.Throws<InvalidOperationException>(() => mgr.InvokeCallbacks(1));
        }

        [Fact]
        public void DisposingOneCallback_DoesNotAffectOthers()
        {
            var mgr = new CallbackManager<int>();
            int count1 = 0, count2 = 0;
            var sub1 = mgr.AddTemporaryCallback(_ => count1++);
            mgr.AddTemporaryCallback(_ => count2++);

            sub1.Dispose();
            mgr.InvokeCallbacks(1);

            Assert.Equal(0, count1);
            Assert.Equal(1, count2);
        }

        [Fact]
        public void ConcurrentAddAndInvoke_DoesNotThrow()
        {
            var mgr = new CallbackManager<int>();
            int total = 0;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Add callbacks from multiple threads while invoking
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100 && !cts.Token.IsCancellationRequested; j++)
                    {
                        var sub = mgr.AddTemporaryCallback(v => Interlocked.Add(ref total, v));
                        mgr.InvokeCallbacks(1);
                        sub.Dispose();
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());
            Assert.True(total > 0);
        }
    }

    public class NonGenericCallbackManagerTests
    {
        [Fact]
        public void InvokeCallbacks_NoPermanent_NoTemporary_DoesNotThrow()
        {
            var mgr = new CallbackManager();
            mgr.InvokeCallbacks();
        }

        [Fact]
        public void SetPermanentCallback_InvokedOnInvoke()
        {
            var mgr = new CallbackManager();
            int callCount = 0;
            mgr.SetPermanentCallback(() => callCount++);

            mgr.InvokeCallbacks();

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void AddTemporaryCallback_InvokedAndDisposable()
        {
            var mgr = new CallbackManager();
            int callCount = 0;
            var sub = mgr.AddTemporaryCallback(() => callCount++);

            mgr.InvokeCallbacks();
            Assert.Equal(1, callCount);

            sub.Dispose();
            mgr.InvokeCallbacks();
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void TemporaryCallback_ExceptionSwallowed()
        {
            var mgr = new CallbackManager();
            int callCount = 0;
            mgr.AddTemporaryCallback(() => throw new Exception("fail"));
            mgr.AddTemporaryCallback(() => callCount++);

            mgr.InvokeCallbacks();

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void PermanentAndTemporary_OrderPreserved()
        {
            var mgr = new CallbackManager();
            var order = new List<string>();

            mgr.SetPermanentCallback(() => order.Add("permanent"));
            mgr.AddTemporaryCallback(() => order.Add("temp"));

            mgr.InvokeCallbacks();

            Assert.Equal(["permanent", "temp"], order);
        }
    }
}
