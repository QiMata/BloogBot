using System.Threading;
using System.Threading.Tasks;
using PromptHandlingService;
using PromptHandlingService.Providers;

namespace PromptHandlingService.Tests;

/// <summary>
/// PHS-MISS-002: Transfer-contract tests for TransferHistory, TransferChatHistory, TransferPromptRunner.
/// PHS-MISS-003: System prompt preservation and InitializeChat semantics.
/// </summary>
public class PromptFunctionBaseTransferTests
{
    #region Test double

    private sealed class TestPromptFunction : PromptFunctionBase
    {
        private readonly string _systemPrompt;
        public int InitializeChatCallCount { get; private set; }

        public TestPromptFunction(IPromptRunner runner, string systemPrompt = "Test system prompt")
            : base(runner)
        {
            _systemPrompt = systemPrompt;
        }

        protected override string SystemPrompt => _systemPrompt;

        public override Task CompleteAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void InitializeChat()
        {
            InitializeChatCallCount++;
        }

        /// <summary>Exposes chat history for test assertions via PrintChatHistory.</summary>
        public string GetPrintedHistory() => PrintChatHistory();
    }

    /// <summary>Non-PromptFunctionBase implementation for negative testing.</summary>
    private sealed class ForeignPromptFunction : IPromptFunction
    {
        public Task CompleteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void ResetChat() { }
        public void SetParameter<T>(string? name = null, T? value = default) { }
        public T GetParameter<T>(string name) => default!;
        public void TransferHistory(IPromptFunction transferTarget) { }
    }

    private static IPromptRunner CreateRunner() => new FakePromptRunner();

    #endregion

    // ===== PHS-MISS-002: TransferHistory =====

    [Fact]
    public void TransferHistory_ThrowsArgumentException_ForNonPromptFunctionBase()
    {
        var source = new TestPromptFunction(CreateRunner());
        var target = new ForeignPromptFunction();

        var ex = Assert.Throws<ArgumentException>(() => source.TransferHistory(target));
        Assert.Contains("PromptFunctionBase", ex.Message);
    }

    [Fact]
    public void TransferHistory_CopiesNonSystemMessages_ToTarget()
    {
        var source = new TestPromptFunction(CreateRunner(), "Source prompt");
        source.ResetChat(); // adds System message + calls InitializeChat
        source.AddChatMessage("User", "Hello");
        source.AddChatMessage("Assistant", "Hi there");

        var target = new TestPromptFunction(CreateRunner(), "Target prompt");

        source.TransferHistory(target);

        var history = target.GetPrintedHistory();
        Assert.Contains("User: Hello", history);
        Assert.Contains("Assistant: Hi there", history);
    }

    [Fact]
    public void TransferHistory_SkipsSystemMessages()
    {
        var source = new TestPromptFunction(CreateRunner(), "Source prompt");
        source.ResetChat(); // inserts System: Source prompt
        source.AddChatMessage("User", "Question");
        source.AddChatMessage("Assistant", "Answer");

        var target = new TestPromptFunction(CreateRunner(), "Target prompt");

        source.TransferHistory(target);

        var history = target.GetPrintedHistory();
        Assert.DoesNotContain("System:", history);
        Assert.DoesNotContain("Source prompt", history);
    }

    [Fact]
    public void TransferHistory_ClearsExistingTargetHistory()
    {
        var source = new TestPromptFunction(CreateRunner());
        source.AddChatMessage("User", "New message");

        var target = new TestPromptFunction(CreateRunner());
        target.AddChatMessage("User", "Old message");

        source.TransferHistory(target);

        var history = target.GetPrintedHistory();
        Assert.DoesNotContain("Old message", history);
        Assert.Contains("New message", history);
    }

    [Fact]
    public void TransferHistory_PreservesMessageOrder()
    {
        var source = new TestPromptFunction(CreateRunner());
        source.AddChatMessage("User", "First");
        source.AddChatMessage("Assistant", "Second");
        source.AddChatMessage("User", "Third");

        var target = new TestPromptFunction(CreateRunner());
        source.TransferHistory(target);

        var history = target.GetPrintedHistory();
        var firstIdx = history.IndexOf("First");
        var secondIdx = history.IndexOf("Second");
        var thirdIdx = history.IndexOf("Third");

        Assert.True(firstIdx < secondIdx, "First should appear before Second");
        Assert.True(secondIdx < thirdIdx, "Second should appear before Third");
    }

    // ===== PHS-MISS-002: TransferPromptRunner =====

    [Fact]
    public void TransferPromptRunner_CopiesRunnerReference()
    {
        var runner1 = CreateRunner();
        var runner2 = CreateRunner();
        var source = new TestPromptFunction(runner1);
        var target = new TestPromptFunction(runner2);

        Assert.NotSame(source.PromptRunner, target.PromptRunner);

        source.TransferPromptRunner(target);

        Assert.Same(runner1, target.PromptRunner);
    }

    // ===== PHS-MISS-003: TransferChatHistory + SystemPrompt =====

    [Fact]
    public void TransferChatHistory_InsertsTargetSystemPrompt_NotSource()
    {
        var source = new TestPromptFunction(CreateRunner(), "Source system prompt");
        source.ResetChat();
        source.AddChatMessage("User", "Hello");

        var target = new TestPromptFunction(CreateRunner(), "Target system prompt");

        source.TransferChatHistory(target);

        var history = target.GetPrintedHistory();
        Assert.Contains("Target system prompt", history);
        Assert.DoesNotContain("Source system prompt", history);
    }

    [Fact]
    public void TransferChatHistory_RemovesAllSourceSystemMessages()
    {
        var source = new TestPromptFunction(CreateRunner(), "Source prompt");
        source.ResetChat(); // System message added
        source.AddChatMessage("User", "Q1");
        source.AddChatMessage("Assistant", "A1");

        var target = new TestPromptFunction(CreateRunner(), "My target prompt");
        source.TransferChatHistory(target);

        // History should have exactly one System entry (target's prompt)
        var history = target.GetPrintedHistory();
        var systemCount = history.Split('\n').Count(l => l.StartsWith("System:"));
        Assert.Equal(1, systemCount);
        Assert.Contains("My target prompt", history);
    }

    [Fact]
    public void TransferChatHistory_SystemPromptIsFirstEntry()
    {
        var source = new TestPromptFunction(CreateRunner());
        source.AddChatMessage("User", "Before system");

        var target = new TestPromptFunction(CreateRunner(), "First entry");
        source.TransferChatHistory(target);

        var history = target.GetPrintedHistory();
        Assert.StartsWith("System: First entry", history);
    }

    [Fact]
    public void TransferChatHistory_CallsInitializeChat_ExactlyOnce()
    {
        var source = new TestPromptFunction(CreateRunner());
        source.AddChatMessage("User", "Hello");

        var target = new TestPromptFunction(CreateRunner());
        var countBefore = target.InitializeChatCallCount;

        source.TransferChatHistory(target);

        Assert.Equal(countBefore + 1, target.InitializeChatCallCount);
    }

    [Fact]
    public void TransferChatHistory_ClearsTargetHistoryFirst()
    {
        var source = new TestPromptFunction(CreateRunner());
        source.AddChatMessage("User", "New");

        var target = new TestPromptFunction(CreateRunner());
        target.AddChatMessage("User", "Old stale data");

        source.TransferChatHistory(target);

        var history = target.GetPrintedHistory();
        Assert.DoesNotContain("Old stale data", history);
        Assert.Contains("New", history);
    }

    [Fact]
    public void TransferChatHistory_PreservesNonSystemMessageOrder()
    {
        var source = new TestPromptFunction(CreateRunner());
        source.AddChatMessage("User", "Alpha");
        source.AddChatMessage("Assistant", "Beta");
        source.AddChatMessage("User", "Gamma");

        var target = new TestPromptFunction(CreateRunner(), "Target prompt");
        source.TransferChatHistory(target);

        var history = target.GetPrintedHistory();
        var alphaIdx = history.IndexOf("Alpha");
        var betaIdx = history.IndexOf("Beta");
        var gammaIdx = history.IndexOf("Gamma");

        Assert.True(alphaIdx < betaIdx);
        Assert.True(betaIdx < gammaIdx);
    }

    // ===== PHS-MISS-003: ResetChat + InitializeChat =====

    [Fact]
    public void ResetChat_InsertsSystemPrompt_AndCallsInitializeChat()
    {
        var func = new TestPromptFunction(CreateRunner(), "Reset prompt");
        func.AddChatMessage("User", "Leftover");

        func.ResetChat();

        var history = func.GetPrintedHistory();
        Assert.StartsWith("System: Reset prompt", history);
        Assert.DoesNotContain("Leftover", history);
        Assert.True(func.InitializeChatCallCount >= 1);
    }

    [Fact]
    public void ResetChat_MultipleCalls_DoNotAccumulate()
    {
        var func = new TestPromptFunction(CreateRunner(), "Prompt");
        func.ResetChat();
        func.ResetChat();
        func.ResetChat();

        var history = func.GetPrintedHistory();
        var systemCount = history.Split('\n').Count(l => l.StartsWith("System:"));
        Assert.Equal(1, systemCount);
    }
}
