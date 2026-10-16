using Jint;

namespace GoatFarm.Tests.Assistant;

public class AssistantVoiceTests
{
    [Fact]
    public void SpeechText_ReadsSupportedBlocks_AndSkipsUnsafeOrUnknown()
    {
        var js = VoiceEngine();
        var spoken = js.Evaluate("""
            assistantSpeechText([
              { kind: 'Text', title: 'Note', text: 'Herd milk was recorded.' },
              { kind: 'Summary', title: 'Milk last month', text: '', summary: { items: [
                { label: 'Produced', value: '120 L' },
                { label: 'Sold', value: '80 L', note: 'Shop sales' }
              ] } },
              { kind: 'Table', title: 'Rows', table: { columns: ['Date', 'Litres'], rows: [['2026-09-01', '12']] } },
              { kind: 'Chart', title: 'Week', chart: { points: [{ label: 'Week 1', value: 40 }, { label: 'Week 2', value: 12 }] } },
              { kind: 'Navigate', navigation: { label: 'Open Milk ledger', path: '/Milk' } },
              { kind: 'Navigate', navigation: { label: 'Evil', path: 'javascript:alert(1)' } },
              { kind: 'Navigate', navigation: { label: 'https://evil.example/Milk', path: '/Milk' } },
              { kind: 'Clarify', clarification: { questions: ['Use last month?'] } },
              { kind: 'Error', title: 'Could not answer', text: 'Sample error', errorCode: 'provider_error' },
              { kind: 'Secret', text: 'should-not-speak' },
              null,
              'nope'
            ])
            """).AsString();

        Assert.Contains("Herd milk was recorded.", spoken);
        Assert.Contains("Produced, 120 L.", spoken);
        Assert.Contains("Sold, 80 L.", spoken);
        Assert.Contains("Shop sales", spoken);
        Assert.Contains("Columns: Date, Litres.", spoken);
        Assert.Contains("Row 1: Date 2026-09-01, Litres 12.", spoken);
        Assert.Contains("Week 1, 40.", spoken);
        Assert.Contains("Open Milk ledger.", spoken);
        Assert.Contains("Use last month?", spoken);
        Assert.Contains("Sample error", spoken);
        Assert.DoesNotContain("javascript", spoken, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", spoken, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("should-not-speak", spoken);
        Assert.DoesNotContain("provider_error", spoken);
        Assert.DoesNotContain("/Milk", spoken);
        Assert.DoesNotContain("trend", spoken, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("increased", spoken, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpeechText_StripsMarkup_BoundsTables_AndTruncates()
    {
        var js = VoiceEngine();
        js.Execute("""
            var rows = [];
            for (var i = 0; i < 12; i++) rows.push(['r' + i, 'v' + i, 'x']);
            var longText = Array(400).join('word ');
            var spoken = assistantSpeechText([
              { kind: 'text', text: '<img src=x onerror=alert(1)>Safe <b>figure</b>' },
              { kind: 'table', table: { columns: ['A','B','C','D','E','F','G'], rows: rows } },
              { kind: 'text', text: longText }
            ]);
            """);

        var spoken = js.GetValue("spoken").AsString();
        Assert.Contains("Safe figure", spoken);
        Assert.DoesNotContain("<img", spoken);
        Assert.DoesNotContain("onerror", spoken);
        Assert.Contains("Additional table cells were omitted.", spoken);
        Assert.Contains("Speech truncated.", spoken);
        Assert.True(spoken.Length < 1300);
    }

    [Fact]
    public void SpeechText_IgnoresMalformedBlocksAndInternalMetadata()
    {
        var js = VoiceEngine();
        var spoken = js.Evaluate("""
            assistantSpeechText([
              { kind: 'summary', summary: 'nope' },
              { kind: 'table', table: { columns: null, rows: { 0: ['x'] } } },
              { kind: 'chart', chart: { points: [{ label: 'Bad', value: 'NaN' }, { label: 'Ok', value: 3 }] } },
              { kind: 'error', text: 'Host=db;Password=secret', errorCode: 'provider_error' },
              { kind: 'navigate', navigation: { label: 'Open Accounting', path: '/Finance?month=2026-09' } }
            ])
            """).AsString();

        Assert.Contains("Ok, 3.", spoken);
        Assert.Contains("Open Accounting.", spoken);
        Assert.DoesNotContain("Password", spoken);
        Assert.DoesNotContain("NaN", spoken);
        Assert.DoesNotContain("/Finance", spoken);
    }

    [Fact]
    public void SpeechText_KeepsTableCellsAlignedWithTheirColumns()
    {
        var js = VoiceEngine();
        var spoken = js.Evaluate("""
            assistantSpeechText([
              { kind: 'table', table: {
                  columns: ['https://evil.example/ledger', 'Public'],
                  rows: [['Rs 999,999', '12 L']]
              } }
            ])
            """).AsString();

        Assert.Contains("Columns: Public.", spoken);
        Assert.Contains("Public 12 L", spoken);
        Assert.DoesNotContain("999,999", spoken);
        Assert.DoesNotContain("evil.example", spoken, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpeechText_StaffFinanceDenialAndRedactedDashboard_DoNotSpeakProtectedFigures()
    {
        var js = VoiceEngine();
        var denied = js.Evaluate("""
            assistantSpeechText([
              { kind: 'Error', title: 'Permission needed', text: 'You do not have permission to view Finance. Ask an admin if you need access.', errorCode: 'permission_denied' }
            ])
            """).AsString();
        Assert.Contains("do not have permission to view Finance", denied);
        Assert.DoesNotContain("900", denied);
        Assert.DoesNotContain("get_finance_summary", denied);
        Assert.DoesNotContain("permission_denied", denied);
        Assert.DoesNotContain("SqlException", denied, StringComparison.OrdinalIgnoreCase);

        var dashboard = js.Evaluate("""
            assistantSpeechText([
              { kind: 'Summary', title: 'Dashboard 2026-10 (finance hidden)',
                text: 'Income, expenses, profit and capital are hidden — they need Finance access.',
                summary: { items: [
                  { label: 'Herd', value: '12' },
                  { label: 'Milk produced', value: '40 L' },
                  { label: 'Milk sold', value: '0 L' },
                  { label: 'Vaccines due', value: '2' }
                ] } },
              { kind: 'Chart', title: 'Milk last 7 days', chart: { points: [{ label: 'Mon', value: 40 }] } }
            ])
            """).AsString();
        Assert.Contains("Herd, 12.", dashboard);
        Assert.Contains("Milk produced, 40 L.", dashboard);
        Assert.DoesNotContain("900", dashboard);
        Assert.DoesNotContain("500", dashboard);
        Assert.DoesNotContain("400", dashboard);
        Assert.DoesNotContain("900000", dashboard);
        Assert.DoesNotContain("30,000", dashboard);
        Assert.DoesNotContain("Rs", dashboard);

        var manager = js.Evaluate("""
            assistantSpeechText([
              { kind: 'Summary', title: 'Profit and loss for 2026-09',
                summary: { items: [
                  { label: 'Income', value: 'Rs 900' },
                  { label: 'Expenses', value: 'Rs 500' },
                  { label: 'Profit', value: 'Rs 400' }
                ] } }
            ])
            """).AsString();
        Assert.Contains("Income, Rs 900.", manager);
        Assert.Contains("Expenses, Rs 500.", manager);
        Assert.Contains("Profit, Rs 400.", manager);
    }

    [Theory]
    [InlineData("not-allowed", "Microphone access was denied.")]
    [InlineData("audio-capture", "No microphone was found.")]
    [InlineData("no-speech", "No speech was detected.")]
    [InlineData("network", "Speech recognition lost its network connection.")]
    [InlineData("aborted", "")]
    public void RecognitionErrors_AreSafeUserMessages(string code, string expected) =>
        Assert.Equal(expected, VoiceEngine().Invoke("assistantRecognitionError", code).AsString());

    [Fact]
    public void MergeTranscript_DoesNotEraseDraft()
    {
        var js = VoiceEngine();
        Assert.Equal("Show milk", js.Invoke("assistantMergeTranscript", "Show", "milk").AsString());
        Assert.Equal("milk last month", js.Invoke("assistantMergeTranscript", "", "milk last month").AsString());
        Assert.Equal("keep", js.Invoke("assistantMergeTranscript", "keep", "").AsString());
    }

    [Fact]
    public void UnsupportedSpeech_LeavesTypedChatAlone()
    {
        var js = Session(withRecognition: false, withSynth: false);
        js.Execute("voice.startListening(); voice.speakLatest([{ kind: 'Text', text: 'Hi' }]);");
        Assert.Equal("", js.GetValue("input").AsString());
        Assert.Equal(0, (int)js.Evaluate("spoken.length").AsNumber());
        Assert.Contains("not available", LastStatus(js), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Microphone_CreatesOneRecognitionInstance_AndDoesNotAutoSubmit()
    {
        var js = Session();
        js.Execute("voice.startListening();");
        Assert.Equal(1, (int)js.Evaluate("FakeRec.instances.length").AsNumber());
        js.Execute("""
            var rec = FakeRec.instances[0];
            firePartial(rec, 'partial should not send');
            fireFinal(rec, 'Show milk production');
            """);
        Assert.Equal("Show milk production", js.GetValue("input").AsString());
        Assert.Equal(0, (int)js.Evaluate("sent.length").AsNumber());
        Assert.False(js.Evaluate("FakeRec.instances[0].continuous").AsBoolean());
        Assert.True(js.Evaluate("FakeRec.instances[0].interimResults").AsBoolean());
        Assert.True(js.Evaluate("voice.isListening()").AsBoolean());
        js.Execute("voice.startListening();");
        Assert.Equal(1, (int)js.Evaluate("FakeRec.instances.length").AsNumber());
        Assert.False(js.Evaluate("voice.isListening()").AsBoolean());
    }

    [Fact]
    public void FinalTranscript_StaysEditable_AndAppendsToDraft()
    {
        var js = Session();
        js.Execute("""
            input = 'Show';
            voice.startListening();
            fireFinal(FakeRec.instances[0], 'milk for last month');
            input = input + ' please';
            """);
        Assert.Equal("Show milk for last month please", js.GetValue("input").AsString());
        Assert.Equal(0, (int)js.Evaluate("sent.length").AsNumber());
    }

    [Fact]
    public void StopListening_AndClosedDrawer_IgnoreLateResults()
    {
        var js = Session();
        js.Execute("""
            voice.startListening();
            var rec = FakeRec.instances[0];
            var late = rec.onresult;
            voice.stopListening();
            input = 'keep';
            late({ results: [{ isFinal: true, '0': { transcript: 'late after stop' } }] });
            voice.startListening();
            rec = FakeRec.instances[FakeRec.instances.length - 1];
            late = rec.onresult;
            open = false;
            voice.onClose();
            late({ results: [{ isFinal: true, '0': { transcript: 'late after close' } }] });
            """);
        Assert.Equal("keep", js.GetValue("input").AsString());
        Assert.True(js.GetValue("cancelled").AsNumber() >= 1);
    }

    [Fact]
    public void PermissionDenied_ShowsSafeStatus()
    {
        var js = Session();
        js.Execute("""
            voice.startListening();
            FakeRec.instances[0].onerror({ error: 'not-allowed' });
            """);
        Assert.Equal("Microphone access was denied.", LastStatus(js));
        Assert.Equal("", js.GetValue("input").AsString());
    }

    [Fact]
    public void Speak_ReadsLatestResponse_StopCancels_AndNewUtteranceCancelsPrevious()
    {
        var js = Session();
        js.Execute("""
            voice.setSpeakPref(false);
            voice.setLastBlocks([{ kind: 'Text', text: 'First reply' }]);
            voice.speakLatest();
            voice.setLastBlocks([{ kind: 'Text', text: 'Second reply' }]);
            voice.speakLatest();
            voice.stopSpeaking();
            """);
        Assert.Equal(2, (int)js.Evaluate("spoken.length").AsNumber());
        Assert.Equal("First reply", js.Evaluate("spoken[0]").AsString());
        Assert.Equal("Second reply", js.Evaluate("spoken[1]").AsString());
        Assert.True(js.Evaluate("cancelled").AsNumber() >= 2);
    }

    [Fact]
    public void SpeakPreference_OnlyAutoSpeaksWhenEnabled()
    {
        var js = Session();
        js.Execute("voice.setLastBlocks([{ kind: 'Text', text: 'Silent until opted in' }]);");
        Assert.Equal(0, (int)js.Evaluate("spoken.length").AsNumber());
        js.Execute("""
            voice.setSpeakPref(true);
            voice.setLastBlocks([{ kind: 'Text', text: 'Opted in reply' }]);
            """);
        Assert.Equal("Opted in reply", js.Evaluate("spoken[0]").AsString());
    }

    [Fact]
    public void ClosedDrawer_DoesNotSpeak_AndKeepsLatestReplyForLater()
    {
        var js = Session();
        js.Execute("""
            voice.setSpeakPref(true);
            open = false;
            voice.setLastBlocks([{ kind: 'Text', text: 'Should not speak while closed' }]);
            """);
        Assert.Equal(0, (int)js.Evaluate("spoken.length").AsNumber());
        js.Execute("""
            open = true;
            voice.speakLatest();
            """);
        Assert.Equal("Should not speak while closed", js.Evaluate("spoken[0]").AsString());
    }

    [Fact]
    public void StopSpeaking_IgnoresCanceledUtteranceErrors()
    {
        var js = Session();
        js.Execute("""
            voice.speakLatest([{ kind: 'Text', text: 'Latest reply' }]);
            voice.stopSpeaking();
            if (lastUtt && lastUtt.onerror) lastUtt.onerror({ error: 'canceled' });
            """);
        Assert.Equal("Latest reply", js.Evaluate("spoken[0]").AsString());
        Assert.DoesNotContain("could not be played", LastStatus(js), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SynthesisFailure_DoesNotThrow_AndLeavesInput()
    {
        var js = Session();
        js.Execute("""
            input = 'typed question';
            synth.speak = function () { throw new Error('no voices'); };
            voice.setLastBlocks([{ kind: 'Text', text: 'Latest' }]);
            voice.speakLatest();
            """);
        Assert.Equal("typed question", js.GetValue("input").AsString());
        Assert.Contains("could not be played", LastStatus(js), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusMessages_AreKeyboardAccessibleCopy()
    {
        var panel = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "GoatFarm.Web", "Views", "Shared", "_AssistantPanel.cshtml"));
        Assert.Contains("aria-pressed", panel);
        Assert.Contains("aria-describedby=\"aiAssistantVoiceStatus\"", panel);
        Assert.Contains("role=\"status\"", panel);
        Assert.Contains("for=\"aiAssistantSpeakPref\"", panel);
    }

    private static string LastStatus(Engine js)
    {
        js.Execute("var lastStatus = statuses.length ? statuses[statuses.length - 1].message : '';");
        return js.GetValue("lastStatus").AsString();
    }

    private static Engine Session(bool withRecognition = true, bool withSynth = true)
    {
        var engine = VoiceEngine();
        engine.Execute($$"""
            var input = '';
            var sent = [];
            var statuses = [];
            var speakPref = false;
            var open = true;
            var cancelled = 0;
            var spoken = [];
            function FakeRec() {
              FakeRec.instances.push(this);
              this.continuous = true;
              this.interimResults = false;
              this.lang = '';
              this.onresult = null;
              this.onerror = null;
              this.onend = null;
            }
            FakeRec.instances = [];
            FakeRec.prototype.start = function () { this.started = true; };
            FakeRec.prototype.stop = function () { this.stopped = true; };
            FakeRec.prototype.abort = function () { this.aborted = true; };
            function fireFinal(rec, text) {
              rec.onresult({ results: [{ isFinal: true, '0': { transcript: text } }] });
            }
            function firePartial(rec, text) {
              rec.onresult({ results: [{ isFinal: false, '0': { transcript: text } }] });
            }
            var lastUtt = null;
            var synth = {
              cancel: function () { cancelled++; },
              speak: function (u) { spoken.push(u.text); lastUtt = u; }
            };
            var voice = createAssistantVoice({
              SpeechRecognition: {{(withRecognition ? "FakeRec" : "null")}},
              speechSynthesis: {{(withSynth ? "synth" : "null")}},
              createUtterance: function (text) { return { text: text, onerror: null }; },
              isOpen: function () { return open; },
              getInput: function () { return input; },
              setInput: function (v) { input = v; },
              getSpeakPref: function () { return speakPref; },
              setSpeakPref: function (on) { speakPref = !!on; },
              setStatus: function (state, message) { statuses.push({ state: state, message: message }); }
            });
            """);
        return engine;
    }

    private static Engine VoiceEngine()
    {
        var source = File.ReadAllText(GoatRecordsJsPath());
        var start = source.IndexOf("  const assistantSafePath =", StringComparison.Ordinal);
        var end = source.IndexOf("\n  const csvCell =", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Voice helpers are missing from goat-records.js.");
        var engine = new Engine();
        engine.Execute(source[start..end].Replace("const assistantSafePath =", "var assistantSafePath =", StringComparison.Ordinal));
        return engine;
    }

    private static string GoatRecordsJsPath() =>
        Path.Combine(FindRepoRoot(), "src", "GoatFarm.Web", "wwwroot", "js", "goat-records.js");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GoatFarm.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
