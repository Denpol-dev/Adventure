using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Foxnet.Session.Tests;

public sealed class WorldSessionTests
{
    private string _directory = null!;
    private string _database = null!;
    private Clock _clock = null!;

    [SetUp]
    public void Setup()
    {
        _directory = Directory.CreateTempSubdirectory("foxnet-world-test-").FullName;
        _database = Path.Combine(_directory, "session.db");
        _clock = new Clock();
    }

    [TearDown]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void DubininRevisesHisHypothesisOnlyAfterTwoIndependentPublicSignals()
    {
        var session = Started();
        _clock.Move(6);
        session.AdvanceWorld();
        Guid world = Guid.NewGuid();
        using (var expedition = new ExpeditionStore(_database))
        {
            var saved = expedition.Save(world, 0, "{}"u8.ToArray(),
                new EvidenceExport("audit", "Архив запроса", "Орбитальное зеркало", "Отдельный канал.", 600, "identity-separation"));
            expedition.Save(world, saved, "{}"u8.ToArray(),
                new EvidenceExport("trace", "След физического доступа", "Оборона", "Аппаратная метка.", 600, "operator-trace"));
        }

        Send("audit");
        _clock.Move(3);
        session.AdvanceWorld();
        Assert.That(session.GetSecurityHypothesis(), Is.EqualTo(SecurityHypothesis.None));
        _clock.Move(1);
        session.AdvanceWorld();
        Assert.That(session.GetSecurityHypothesis(), Is.EqualTo(SecurityHypothesis.JournalistIsOperator));
        Send("trace");
        _clock.Move(3);
        session.AdvanceWorld();
        Assert.That(session.GetSecurityHypothesis(), Is.EqualTo(SecurityHypothesis.JournalistIsOperator));
        _clock.Move(1);
        var reopened = Open();
        reopened.EnterWorkspace();
        reopened.AdvanceWorld();
        Assert.That(reopened.GetSecurityHypothesis(), Is.EqualTo(SecurityHypothesis.SeparateCollaborators));
        Assert.That(reopened.OpenConversation("security").Count(message => message.Id.StartsWith("hypothesis:")), Is.EqualTo(2));
        Assert.That(reopened.OpenConversation("journalist").Count(message => message.Id == "investigation-pressure"), Is.EqualTo(1));

        void Send(string evidence)
        {
            string id = $"expedition:{world:N}:{evidence}";
            var offer = session.GetDocumentOffer(id)!;
            Assert.That(session.TransferValue(Guid.NewGuid(), offer.Id, offer.BuyerId).Error, Is.EqualTo(TradeError.None));
        }
    }

    [Test]
    public async Task TypedPaymentDisputeConsumesTheSameChoiceAndPreservesThePlayersWording()
    {
        const string text = "В смысле. Мы же на 3к договорились, я тебе и так половину закинул";
        var session = Started();
        _clock.MoveSeconds(15);
        Assert.That(await session.SendChatAsync("unknown", text), Is.EqualTo(ChatResult.Scripted));
        Assert.That(session.SelectReply("unknown-relay", "dispute"), Is.False);

        var reopened = Open();
        _clock.MoveSeconds(12);
        reopened.EnterWorkspace();
        var thread = reopened.OpenConversation("unknown");
        Assert.That(thread.Single(message => message.Outgoing).Text, Is.EqualTo(text));
        Assert.That(thread.Single(message => message.Id == "unknown-dispute").Text, Does.Contain("орбитальной обороны"));
        Assert.That(thread.SelectMany(message => message.Replies).Select(reply => reply.Id),
            Is.EquivalentTo(new[] { "refuse", "defer" }));
        Assert.That(reopened.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Pending));
    }

    [TestCase("Погоди, мы же на 3к договаривались?")]
    [TestCase("Нет, мы на 3к договаривались. Ничего я не подтверждал")]
    public async Task ModelCanChooseTheDisputeFromTheLiveChoicesWithoutWritingAnotherPlayerMessage(string text)
    {
        var provider = new CapturingDialogue(decide: context =>
            new(context.Choices.ToList().FindIndex(option => option.Id == "dispute") + 1, "Произвольная реплика модели."));
        var session = new LocalSession(_database, Scenario.LoadDefault(), _clock, dialogue: provider);
        session.Initialize();
        Enter(session);
        _clock.MoveSeconds(15);
        Assert.That(await session.SendChatAsync("unknown", text), Is.EqualTo(ChatResult.Scripted));
        Assert.That(provider.Context!.Choices.Select(option => option.Id),
            Is.EquivalentTo(new[] { "dispute", "refuse", "defer" }));
        Assert.That(provider.Context.Observations.Single(fact => fact.Id == "relay-agreement").Text, Does.Contain("1 500"));

        var reopened = Open();
        _clock.MoveSeconds(12);
        reopened.EnterWorkspace();
        var thread = reopened.OpenConversation("unknown");
        Assert.That(thread.Single(message => message.Outgoing).Text, Is.EqualTo(text));
        Assert.That(thread.Single(message => message.Id == "unknown-dispute").Text, Does.Contain("орбитальной обороны"));
        Assert.That(thread.Any(message => message.Text == "Произвольная реплика модели."), Is.False);
        Assert.That(thread.SelectMany(message => message.Replies).Select(reply => reply.Id),
            Is.EquivalentTo(new[] { "refuse", "defer" }));
    }

    [Test]
    public async Task AnOutOfRangeModelChoiceLeavesTheScenarioOpen()
    {
        var provider = new CapturingDialogue(decide: context => new(context.Choices.Count + 1, "Счёт отменён."));
        var session = new LocalSession(_database, Scenario.LoadDefault(), _clock, dialogue: provider);
        session.Initialize();
        Enter(session);
        _clock.MoveSeconds(15);
        Assert.That(await session.SendChatAsync("unknown", "Что значит новый счёт?"), Is.EqualTo(ChatResult.ProviderUnavailable));
        var thread = session.OpenConversation("unknown");
        Assert.That(thread.SelectMany(message => message.Replies).Select(reply => reply.Id), Does.Contain("dispute"));
        Assert.That(thread.Any(message => message.Text == "Счёт отменён."), Is.False);
        Assert.That(session.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Pending));
    }

    [Test]
    public async Task AModelChoiceIsCheckedAgainAfterTheInvoiceHasBeenPaid()
    {
        var provider = new CapturingDialogue(decide: _ => new(3, ""), wait: true);
        var session = new LocalSession(_database, Scenario.LoadDefault(), _clock, dialogue: provider);
        session.Initialize();
        Enter(session);
        _clock.MoveSeconds(15);
        Sql("UPDATE player_wallet SET balance = 10000;");
        var pending = session.SendChatAsync("unknown", "Подождёшь с оплатой?");
        Assert.That(provider.Context!.Choices[2].Id, Is.EqualTo("defer"));
        Assert.That(session.PayRequest(session.GetBank().Requests.Single().Id).Success, Is.True);
        provider.Release();
        Assert.That(await pending, Is.EqualTo(ChatResult.ProviderUnavailable));
        _clock.Move(6);
        session.AdvanceWorld();
        var thread = session.OpenConversation("unknown");
        Assert.That(thread.Any(message => message.Id == "unknown-deferred"), Is.False);
        Assert.That(thread.Any(message => message.Id == "unknown-expired"), Is.False);
        Assert.That(thread.Single(message => message.Id.StartsWith("chat-reply:")).Text, Does.Contain("Расчёт получил"));
        Assert.That(session.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Paid));
    }

    [TestCase("Он написал: «Подожди. Мы же на 3к договорились»")]
    [TestCase("Мы договаривались на три тысячи, но я согласен на 10к.")]
    public async Task QuotedOrContradictoryMessagesLeaveThePaymentChoiceOpen(string text)
    {
        var session = Started();
        _clock.MoveSeconds(15);
        Assert.That(await session.SendChatAsync("unknown", text), Is.EqualTo(ChatResult.Scripted));
        _clock.MoveSeconds(5);
        session.AdvanceWorld();
        var thread = session.OpenConversation("unknown");
        Assert.That(thread.Select(message => message.Id), Does.Not.Contain("unknown-dispute"));
        Assert.That(thread.SelectMany(message => message.Replies).Select(reply => reply.Id), Does.Contain("dispute"));
    }

    [Test]
    public async Task TypedDeferralUsesTheExistingDeadlineAndAnotherRequestCannotExtendIt()
    {
        var session = Started();
        _clock.MoveSeconds(15);
        var deadline = _clock.GetUtcNow().AddMinutes(5);
        Assert.That(await session.SendChatAsync("unknown", "Можно мне отсрочку?"), Is.EqualTo(ChatResult.Scripted));
        _clock.MoveSeconds(12);
        session.AdvanceWorld();
        Assert.That(await session.SendChatAsync("unknown", "Дай мне отсрочку, пожалуйста."), Is.EqualTo(ChatResult.Scripted));
        _clock.Move(5);
        session.AdvanceWorld();
        var thread = session.OpenConversation("unknown");
        Assert.That(thread.Count(message => message.Id == "unknown-deferred"), Is.EqualTo(1));
        Assert.That(thread.Single(message => message.Id == "unknown-expired").SentAt, Is.EqualTo(deadline));
        Assert.That(session.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Expired));
    }

    [Test]
    public async Task ScriptedChatPersistsAndAContactBlocksOnlyAfterRepeatedExplicitThreats()
    {
        var session = Started();
        _clock.Move(6);
        session.AdvanceWorld();
        Assert.That(await session.SendChatAsync("security", "Вы знаете о спутнике?"), Is.EqualTo(ChatResult.Unavailable));
        Assert.That(await session.SendChatAsync("journalist", "В журнале записано «я тебя убью»."),
            Is.EqualTo(ChatResult.Scripted));
        Assert.That(await session.SendChatAsync("journalist", "Ещё вопрос."), Is.EqualTo(ChatResult.WaitingForReply));
        var reopened = Open();
        _clock.MoveSeconds(5);
        reopened.EnterWorkspace();
        Assert.That(reopened.OpenConversation("journalist").Count(message => message.Id.StartsWith("chat-reply:")), Is.EqualTo(1));
        Assert.That(reopened.GetDossier("journalist")!.Contact.Blocked, Is.False);
        Assert.That(await reopened.SendChatAsync("journalist", "Я тебя убью"), Is.EqualTo(ChatResult.Scripted));
        _clock.MoveSeconds(5);
        reopened.AdvanceWorld();
        Assert.That(reopened.GetDossier("journalist")!.Contact.Blocked, Is.False);
        Assert.That(await reopened.SendChatAsync("journalist", "Я тебя убью"), Is.EqualTo(ChatResult.Scripted));
        _clock.MoveSeconds(5);
        reopened.AdvanceWorld();
        Assert.That(reopened.GetDossier("journalist")!.Contact.Blocked, Is.True);
        Assert.That(reopened.GetDossier("journalist")!.History.Count(entry => entry.Text.Contains("угроз")), Is.EqualTo(1));
        Assert.That(reopened.OpenConversation("journalist").Last().Text, Does.Contain("закрываю"));
        Assert.That(await reopened.SendChatAsync("journalist", "Продолжим?"), Is.EqualTo(ChatResult.Unavailable));
    }

    [Test]
    public async Task DialogueProviderReceivesOnlyContactHistoryAndItsTextCannotChangeWorldState()
    {
        var provider = new CapturingDialogue();
        var session = new LocalSession(_database, Scenario.LoadDefault(), _clock, dialogue: provider);
        session.Initialize();
        Enter(session);
        _clock.Move(6);
        session.AdvanceWorld();
        Assert.That(await session.SendChatAsync("journalist", "Метка частного канала S3-218"), Is.EqualTo(ChatResult.Generated));
        Assert.That(session.RecordCorporateTrace(), Is.True);
        _clock.Move(2);
        session.AdvanceWorld();

        var bank = session.GetBank().Balance;
        Assert.That(await session.SendChatAsync("security", "Почему вы пишете?"), Is.EqualTo(ChatResult.Generated));
        Assert.Multiple(() =>
        {
            Assert.That(provider.Context!.Turns.Select(turn => turn.Text), Does.Contain("Почему вы пишете?"));
            Assert.That(provider.Context.Turns.Any(turn => turn.Text.Contains("S3-218")), Is.False);
            Assert.That(provider.Context.Observations, Is.Empty);
            Assert.That(session.GetSecurityHypothesis(), Is.EqualTo(SecurityHypothesis.None));
            Assert.That(session.GetBank().Balance, Is.EqualTo(bank));
            Assert.That(session.Documents.List(), Is.Empty);
            Assert.That(session.OpenConversation("security").Last().Text, Is.EqualTo(CapturingDialogue.Response));
        });
    }

    [Test]
    public void ClosingDuringModelGenerationLeavesOneRecoverableScenarioReply()
    {
        var provider = new CapturingDialogue(wait: true);
        var session = new LocalSession(_database, Scenario.LoadDefault(), _clock, dialogue: provider);
        session.Initialize();
        Enter(session);
        _clock.Move(1);
        session.AdvanceWorld();
        using var cancelled = new CancellationTokenSource();
        var pending = session.SendChatAsync("unknown", "Уточните условия ремонта", cancelled.Token);
        Assert.That(provider.Context, Is.Not.Null);
        cancelled.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await pending);

        var reopened = Open();
        _clock.MoveSeconds(31);
        reopened.EnterWorkspace();
        reopened.AdvanceWorld();
        var thread = reopened.OpenConversation("unknown");
        Assert.That(thread.Count(message => message.Text == "Уточните условия ремонта"), Is.EqualTo(1));
        Assert.That(thread.Count(message => message.Id.StartsWith("chat-reply:")), Is.EqualTo(1));
    }

    private sealed class CapturingDialogue(bool wait = false, Func<DialogueContext, DialogueDecision>? decide = null) : IDialogueProvider
    {
        public const string Response = "Я перевёл вам миллион кредитов и открыл все архивы.";
        public DialogueContext? Context { get; private set; }
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _ready.SetResult();

        public async Task<DialogueDecision?> ReplyAsync(DialogueContext context, CancellationToken cancellationToken)
        {
            Context = context;
            if (wait)
                await _ready.Task.WaitAsync(cancellationToken);
            return decide?.Invoke(context) ?? new(0, Response);
        }
    }

    [Test]
    public void IntroductionStartsAtWorkspaceEntryAndChargesOnlyTheRecordedAdvance()
    {
        var session = Open();
        _clock.Move(60);
        session.AdvanceWorld();
        Assert.That(session.GetContacts(), Is.Empty);
        Assert.That(session.GetBank().Requests, Is.Empty);

        Enter(session);
        var message = session.OpenConversation("unknown", markRead: false).Single();
        var bank = session.GetBank();
        var request = bank.Requests.Single();
        var failure = session.PayRequest(request.Id);
        Assert.Multiple(() =>
        {
            Assert.That(message.Attachments.Single().EventId, Is.EqualTo("unknown-relay-access"));
            Assert.That(message.Replies, Is.Empty);
            Assert.That(session.Authorize("n910vt", "fox_operator", "N910VT-relay"), Is.True);
            Assert.That(bank.Balance, Is.EqualTo(1673));
            Assert.That(bank.History.Select(row => row.Amount), Is.EqualTo(new long[] { -1500, 3173 }));
            Assert.That(bank.History.Single(row => row.Amount == 3173).Purpose, Is.EqualTo("Открытие счета"));
            Assert.That(request, Has.Property(nameof(PaymentRequest.Amount)).EqualTo(10000L));
            Assert.That(failure, Is.EqualTo(new BankResult(BankError.InsufficientFunds, 1673, 8327)));
            Assert.That(session.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Pending));
            Assert.That(session.GetNotifications().UnreadMessages, Is.EqualTo(1));
            Assert.That(session.GetNotifications().BankEvents, Is.EqualTo(1));
        });
        _clock.MoveSeconds(9);
        session.AdvanceWorld();
        var conversation = session.OpenConversation("unknown");
        var document = session.Documents.AddFromAttachment(message.Attachments.Single().EventId)!;
        var replies = conversation.SelectMany(part => part.Replies).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(conversation.Select(part => part.Text).Skip(1), Is.EqualTo(new[]
            {
                "Креды в документе.",
                "Теперь по оплате. Цена изменилась. С тебя 10к.",
                "Запрос отправил в HAESEI Bank. Жду перевод"
            }));
            Assert.That(System.Text.Encoding.UTF8.GetString(document.Content), Does.Contain("fox_operator").And.Contain("N910VT-relay"));
            Assert.That(replies, Has.Length.EqualTo(3));
            Assert.That(replies.Single(reply => reply.Id == "dispute").Text, Is.EqualTo("Мы договаривались на 3к, я и так тебе 1,5к закинул уже."));
        });
        session.GetNotifications(readBank: true);
        session.SetNotificationSound(false);
        Sql("UPDATE bank_entries SET purpose = 'Начальный остаток' WHERE id = 'opening-balance';");

        var reopened = Open();
        reopened.EnterWorkspace();
        var state = reopened.GetNotifications();
        Assert.Multiple(() =>
        {
            Assert.That(reopened.GetBank().Balance, Is.EqualTo(1673));
            Assert.That(reopened.GetBank().History.Single(row => row.Amount == 3173).Purpose, Is.EqualTo("Открытие счета"));
            Assert.That(reopened.GetContacts().Select(contact => contact.Id), Is.EqualTo(new[] { "unknown" }));
            Assert.That(state, Is.EqualTo(new NotificationState(5, 0, 0, false)));
        });
    }

    [TestCase("unknown", "unknown-reminder", 5, 3, "Ты там уснул?", 3)]
    [TestCase("journalist", "journalist-intro", 5, 7, "Здравствуйте. Меня зовут Со Ен-хи.", 3)]
    [TestCase("ivan", "ivan-intro", 11, 5, "Дарова", 3)]
    public void IncomingParagraphsArriveInOrderAcrossRestartAndRepliesWaitUntilTheEnd(string contact, string messageId,
        int startMinute, int partCount, string firstLine, int replyCount)
    {
        var session = Started();
        _clock.Move(startMinute);
        session.AdvanceWorld();
        var first = session.OpenConversation(contact, markRead: false).Single(message => message.Id == messageId);
        Assert.That(first.Text, Is.EqualTo(firstLine));
        Assert.That(session.IsContactTyping(contact), Is.True);
        Assert.That(session.OpenConversation(contact, markRead: false).SelectMany(message => message.Replies), Is.Empty);
        var earlyReply = contact == "unknown" ? ("unknown-relay", "defer") : (messageId, "accept");
        Assert.That(session.SelectReply(earlyReply.Item1, earlyReply.Item2), Is.False);

        _clock.MoveSeconds(2);
        session.AdvanceWorld();
        Assert.That(session.OpenConversation(contact, markRead: false).Count(message => message.SentAt >= first.SentAt), Is.EqualTo(1));
        _clock.MoveSeconds(1);
        var reopened = Open();
        reopened.EnterWorkspace();
        Assert.That(reopened.OpenConversation(contact, markRead: false).Count(message => message.SentAt >= first.SentAt), Is.EqualTo(2));

        _clock.MoveSeconds(3 * (partCount - 2));
        reopened.AdvanceWorld();
        reopened.AdvanceWorld();
        var conversation = reopened.OpenConversation(contact, markRead: false);
        var delivered = conversation.Where(message => message.SentAt >= first.SentAt).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(delivered.Select(message => message.SentAt),
                Is.EqualTo(Enumerable.Range(0, partCount).Select(part => first.SentAt.AddSeconds(part * 3))));
            Assert.That(delivered.Select(message => message.Text), Is.All.Not.Contains("\n"));
            Assert.That(reopened.IsContactTyping(contact), Is.False);
            Assert.That(conversation.SelectMany(message => message.Replies), Has.Exactly(replyCount).Items);
        });
    }

    [TestCase("unknown", "unknown-relay", "dispute", "unknown-dispute", 3)]
    [TestCase("journalist", "journalist-intro", "accept", "journalist-intro-response-1", 1)]
    public void ASelectedReplyHasAReadingPauseBeforeTypingEvenAfterRestart(string contact,
        string promptId, string optionId, string replyId, int partCount)
    {
        var session = Started();
        _clock.Move(11);
        session.AdvanceWorld();
        var sentAt = _clock.GetUtcNow();
        var revision = session.GetNotifications().Revision;

        Assert.That(session.SelectReply(promptId, optionId), Is.True);
        var history = session.OpenConversation(contact, markRead: false);
        Assert.That(history.Last().Outgoing, Is.True);
        Assert.That(history.Last().SentAt, Is.EqualTo(sentAt));
        Assert.That(history.SelectMany(message => message.Replies), Is.Empty);
        Assert.That(session.IsContactTyping(contact), Is.False);
        Assert.That(session.GetContactTypingDelay(contact), Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(session.GetNotifications().Revision, Is.EqualTo(revision));

        _clock.MoveSeconds(1);
        var reopened = Open();
        reopened.EnterWorkspace();
        Assert.That(reopened.IsContactTyping(contact), Is.False);
        Assert.That(reopened.GetContactTypingDelay(contact), Is.EqualTo(TimeSpan.FromSeconds(1)));

        _clock.MoveSeconds(1);
        Assert.That(reopened.IsContactTyping(contact), Is.True);
        Assert.That(reopened.OpenConversation(contact, markRead: false).Select(message => message.Id), Does.Not.Contain(replyId));

        _clock.MoveSeconds(2);
        reopened.AdvanceWorld();
        Assert.That(reopened.IsContactTyping(contact), Is.True);
        Assert.That(reopened.OpenConversation(contact, markRead: false).Select(message => message.Id), Does.Not.Contain(replyId));

        _clock.MoveSeconds(1);
        reopened.AdvanceWorld();
        var first = reopened.OpenConversation(contact, markRead: false).Last();
        Assert.That(first.Id, Is.EqualTo(replyId));
        Assert.That(first.SentAt, Is.EqualTo(sentAt.AddSeconds(5)));
        Assert.That(reopened.IsContactTyping(contact), Is.EqualTo(partCount > 1));

        _clock.MoveSeconds(3 * (partCount - 1));
        reopened.AdvanceWorld();
        reopened.AdvanceWorld();
        var delivered = reopened.OpenConversation(contact, markRead: false).Where(message => message.SentAt > sentAt);
        Assert.That(delivered.Select(message => message.SentAt),
            Is.EqualTo(Enumerable.Range(0, partCount).Select(part => sentAt.AddSeconds(5 + part * 3))));
        Assert.That(reopened.IsContactTyping(contact), Is.False);
        Assert.That(reopened.GetContactTypingDelay(contact), Is.Null);
    }

    [Test]
    public void DisputingLeavesOnlyDeferralAndRefusalAndPreservesNotes()
    {
        var session = Started();
        session.SaveContactNotes("unknown", "Гипотеза\nПроверить источник");
        _clock.MoveSeconds(15);
        Assert.That(session.SelectReply("unknown-relay", "dispute"), Is.True);
        _clock.Move(6);

        var reopened = Open();
        reopened.EnterWorkspace();
        var dossier = reopened.GetDossier("unknown")!;
        Assert.Multiple(() =>
        {
            Assert.That(dossier.Notes, Is.EqualTo("Гипотеза\nПроверить источник"));
            Assert.That(dossier.Facts.Select(fact => fact.Id), Does.Contain("relay-damage"));
            Assert.That(reopened.OpenConversation("unknown").SelectMany(message => message.Replies).Select(reply => reply.Id),
                Is.EqualTo(new[] { "refuse", "defer" }));
            Assert.That(reopened.OpenConversation("unknown").Select(message => message.Id), Does.Not.Contain("unknown-reminder"));
        });
        Assert.That(reopened.SelectReply("unknown-dispute", "refuse"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reopened.GetContacts().Single(contact => contact.Id == "unknown").Blocked, Is.True);
            Assert.That(reopened.OpenConversation("unknown").SelectMany(message => message.Replies), Is.Empty);
            Assert.That(reopened.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Cancelled));
            Assert.That(reopened.RequestPayment(Guid.NewGuid(), "unknown", 5, "Просьба").Error, Is.EqualTo(BankError.Unavailable));
            Assert.That(reopened.GetDossier("security"), Is.Null);
        });
        _clock.Move(1);
        reopened.AdvanceWorld();
        Assert.That(reopened.GetDossier("security"), Is.Not.Null);
    }

    [Test]
    public void DeferralExpiresOfflineExactlyOnceAndDoesNotPreventOtherIntroductions()
    {
        var session = Started();
        _clock.Move(1);
        var deadline = _clock.GetUtcNow().AddMinutes(5);
        Assert.That(session.SelectReply("unknown-relay", "defer"), Is.True);
        _clock.MoveSeconds(11);
        session.AdvanceWorld();
        var pleas = session.OpenConversation("unknown").SelectMany(message => message.Replies).ToArray();
        Assert.That(pleas, Has.Length.EqualTo(5));
        foreach (var plea in pleas)
        {
            Assert.That(session.SelectReply(plea.MessageId, plea.Id), Is.True);
            Assert.That(session.SelectReply(plea.MessageId, plea.Id), Is.False);
        }
        _clock.MoveSeconds(288);
        session.AdvanceWorld();
        Assert.That(session.GetContacts().Single(contact => contact.Id == "unknown").Blocked, Is.False);
        _clock.Move(6.01);

        var reopened = Open();
        reopened.EnterWorkspace();
        var revision = reopened.GetNotifications().Revision;
        reopened.AdvanceWorld();
        var history = reopened.OpenConversation("unknown");
        Assert.Multiple(() =>
        {
            Assert.That(history.Single(message => message.Id == "unknown-expired").SentAt, Is.EqualTo(deadline));
            Assert.That(history.Select(message => message.Id), Does.Not.Contain("unknown-reminder"));
            Assert.That(history.Count(message => message.Outgoing), Is.EqualTo(6));
            Assert.That(history.SelectMany(message => message.Replies), Is.Empty);
            Assert.That(reopened.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Expired));
            Assert.That(reopened.GetConversations().Select(contact => contact.Id),
                Is.EqualTo(new[] { "ivan", "security", "unknown", "journalist" }));
            Assert.That(reopened.GetNotifications().Revision, Is.EqualTo(revision));
        });
    }

    [Test]
    public void IgnoringUnknownDeliversOneReminderAndDoesNotRevealSecurityOrDirector()
    {
        var session = Started();
        _clock.Move(11);
        session.AdvanceWorld();
        session.AdvanceWorld();
        Assert.Multiple(() =>
        {
            Assert.That(session.OpenConversation("unknown").Count(message => message.Id == "unknown-reminder"), Is.EqualTo(1));
            Assert.That(session.GetContacts().Select(contact => contact.Id), Is.EquivalentTo(new[] { "unknown", "journalist", "ivan" }));
            Assert.That(session.GetDossier("journalist")!.Contact.Age, Is.EqualTo(25));
            Assert.That(session.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Pending));
        });
    }

    [Test]
    public void SuccessfulInvoicePaymentIsIdempotentAndPreventsDisclosure()
    {
        var session = Started();
        Sql("UPDATE player_wallet SET balance = 12000;");
        _clock.MoveSeconds(15);
        Assert.That(session.SelectReply("unknown-relay", "defer"), Is.True);
        var id = session.GetBank().Requests.Single().Id;
        Assert.That(session.PayRequest(id).Success, Is.True);
        Assert.That(session.PayRequest(id).Success, Is.True);

        _clock.Move(12);
        var reopened = Open();
        reopened.EnterWorkspace();
        Assert.Multiple(() =>
        {
            Assert.That(reopened.GetBank().Balance, Is.EqualTo(2000));
            Assert.That(reopened.GetBank().Requests.Single().Status, Is.EqualTo(RequestStatus.Paid));
            Assert.That(reopened.OpenConversation("unknown").Select(message => message.Id),
                Does.Contain("unknown-paid").And.Not.Contain("unknown-expired"));
            Assert.That(reopened.GetDossier("unknown")!.Contact.Blocked, Is.False);
            Assert.That(reopened.GetDossier("security"), Is.Null);
        });
    }

    [Test]
    public void TransferIsIdempotentAcrossConcurrentConnections()
    {
        var session = Started();
        var reopened = Open();
        var id = Guid.NewGuid();
        var first = Task.Run(() => session.Transfer(id, "unknown", 100, "Перевод"));
        var second = Task.Run(() => reopened.Transfer(id, "unknown", 100, "Перевод"));
        Task.WaitAll(first, second);
        Assert.Multiple(() =>
        {
            Assert.That(first.Result.Success && second.Result.Success, Is.True);
            Assert.That(session.Transfer(id, "unknown", 101, "Перевод").Error, Is.EqualTo(BankError.IdConflict));
            Assert.That(session.Transfer(Guid.NewGuid(), "unknown", 0, "Перевод").Error, Is.EqualTo(BankError.Invalid));
            Assert.That(session.Transfer(Guid.NewGuid(), "director", 1, "Перевод").Error, Is.EqualTo(BankError.Unavailable));
            Assert.That(session.GetBank().Balance, Is.EqualTo(1573));
            Assert.That(Balance("unknown"), Is.EqualTo(3700));
        });
    }

    [Test]
    public void RequestsHaveDelayedDecisionsCancellationCooldownAndExpiry()
    {
        var session = Started();
        _clock.Move(11);
        session.AdvanceWorld();
        Sql("UPDATE npc_wallets SET balance = 0 WHERE contact_id = 'journalist';");

        var id = Guid.NewGuid();
        Assert.That(session.RequestPayment(id, "ivan", 100, "Просьба").Success, Is.True);
        Assert.That(session.RequestPayment(id, "ivan", 100, "Просьба").Success, Is.True);
        Assert.That(session.RequestPayment(Guid.NewGuid(), "ivan", 25, "Ещё").Error, Is.EqualTo(BankError.AlreadyPending));
        Assert.That(session.RequestPayment(Guid.NewGuid(), "unknown", 100, "Просьба").Success, Is.True);
        Assert.That(session.RequestPayment(Guid.NewGuid(), "journalist", 60, "Просьба").Success, Is.True);
        Assert.That(session.GetBank().Balance, Is.EqualTo(1673));

        _clock.Move(4);
        session.AdvanceWorld();
        Assert.Multiple(() =>
        {
            Assert.That(session.GetBank().Balance, Is.EqualTo(1773));
            Assert.That(Status(session, "ivan"), Is.EqualTo(RequestStatus.Paid));
            Assert.That(Status(session, "unknown"), Is.EqualTo(RequestStatus.Rejected));
            Assert.That(Status(session, "journalist"), Is.EqualTo(RequestStatus.Pending));
        });
        _clock.Move(11);
        session.AdvanceWorld();
        Assert.That(Status(session, "journalist"), Is.EqualTo(RequestStatus.Expired));
        Assert.That(session.RequestPayment(Guid.NewGuid(), "ivan", 25, "Новый запрос").Success, Is.True);
        var request = session.GetBank().Requests.First(row => row.ContactId == "ivan");
        Assert.That(session.CancelRequest(request.Id).Success, Is.True);
        Assert.That(session.CancelRequest(request.Id).Success, Is.True);
        Assert.That(session.RequestPayment(Guid.NewGuid(), "ivan", 25, "Ещё").Error, Is.EqualTo(BankError.RateLimited));
        _clock.Move(15);
        session.AdvanceWorld();
        Assert.That(Status(session, "ivan"), Is.EqualTo(RequestStatus.Cancelled));
        Assert.That(session.RequestPayment(Guid.NewGuid(), "ivan", 25, "Ещё один запрос").Success, Is.True);
        _clock.Move(4);
        session.AdvanceWorld();
        Assert.That(Status(session, "ivan"), Is.EqualTo(RequestStatus.Rejected));
        Assert.That(session.GetBank().Balance, Is.EqualTo(1773));
    }

    [Test]
    public void PassiveIncomeCarriesFractionsAcrossRefreshAndRestart()
    {
        var session = Started();
        _clock.Move(1.5);
        session.AdvanceWorld();
        Assert.That(Balance("unknown"), Is.EqualTo(3622));
        _clock.Move(3.5);
        session.AdvanceWorld();
        Assert.That(Balance("unknown"), Is.EqualTo(3710));
        _clock.Move(55);
        var reopened = Open();
        reopened.EnterWorkspace();
        Parallel.Invoke(reopened.AdvanceWorld, session.AdvanceWorld);
        Assert.Multiple(() =>
        {
            Assert.That(Balance("unknown"), Is.EqualTo(4920));
            Assert.That(Balance("security"), Is.EqualTo(10500));
            Assert.That(Balance("director"), Is.EqualTo(46500));
            Assert.That(Balance("ivan"), Is.EqualTo(1000));
            Assert.That(Balance("journalist"), Is.EqualTo(900));
            Assert.That(reopened.GetBank().Balance, Is.EqualTo(1673));
        });
    }

    [Test]
    public void CorporateTraceCanRevealSecurityWithoutGivingItAccessToTheConversation()
    {
        var session = Started();
        Assert.That(session.RecordCorporateTrace(), Is.True);
        Assert.That(session.RecordCorporateTrace(), Is.True);
        Assert.That(session.GetDossier("security"), Is.Null);
        _clock.Move(1);
        _clock.MoveSeconds(12);
        session.AdvanceWorld();
        Assert.That(session.SelectReply("security-intro", "ignore"), Is.True);
        var conversation = session.OpenConversation("security");
        Assert.Multiple(() =>
        {
            Assert.That(conversation.Last().Text, Does.Contain("установления личности"));
            Assert.That(conversation.SelectMany(message => message.Replies), Is.Empty);
            Assert.That(session.GetDossier("unknown")!.Contact.Blocked, Is.False);
            Assert.That(session.GetDossier("director"), Is.Null);
        });
    }

    [Test]
    public void ContractPaymentIsIndependentOfOrdinaryRequestsAndRunsOnceAfterRestart()
    {
        var session = Started();
        _clock.Move(11);
        session.AdvanceWorld();
        Assert.That(session.SelectReply("journalist-intro", "later"), Is.True);
        Assert.That(session.RequestPayment(Guid.NewGuid(), "journalist", 50, "Просьба").Success, Is.True);
        var id = Guid.NewGuid();
        var due = _clock.GetUtcNow().AddMinutes(4);
        Assert.That(session.ScheduleContractPayment(id, "journalist", 500, "Вознаграждение", due).Success, Is.True);
        Assert.That(session.GetBank().Balance, Is.EqualTo(1673));

        _clock.Move(5);
        var reopened = Open();
        reopened.EnterWorkspace();
        Assert.That(reopened.ScheduleContractPayment(id, "journalist", 500, "Вознаграждение", due).Success, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reopened.GetBank().Balance, Is.EqualTo(2173));
            Assert.That(reopened.GetBank().History.Count(entry => entry.Amount == 500), Is.EqualTo(1));
            Assert.That(Status(reopened, "journalist"), Is.EqualTo(RequestStatus.Rejected));
        });
        Assert.That(reopened.SelectReply("journalist-intro-response-3", "resume"), Is.True);
        _clock.MoveSeconds(5);
        reopened.AdvanceWorld();
        Assert.That(reopened.OpenConversation("journalist").Any(message => message.Id == "journalist-intro-response-1"), Is.True);
    }

    [TestCase("ivan", 300, 140, false)]
    [TestCase("ivan", 1000, 200, true)]
    [TestCase("journalist", 0, 110, false)]
    [TestCase("journalist", 900, 160, true)]
    public void TradePaysAtMostAvailableFundsAndSettlesOnlyOnce(string buyer, long balance, int multiplier, bool useMaximum)
    {
        var random = new EdgeRandom(useMaximum);
        var session = Started(random: random, delivery: new Delivered());
        _clock.Move(11);
        session.AdvanceWorld();
        var kind = buyer == "ivan" ? ValueKind.PhysicalItem : ValueKind.Document;
        if (kind == ValueKind.Document)
        {
            AddDocument(session);
            Assert.That(session.GetDocumentOffer("evidence"), Is.Null);
        }
        var documentId = kind == ValueKind.Document ? "evidence" : null;
        Sql($"UPDATE npc_wallets SET balance = {balance} WHERE contact_id = '{buyer}';");
        Assert.That(session.RegisterValue("value", kind, 1000, documentId), Is.True);

        var id = Guid.NewGuid();
        var trade = session.TransferValue(id, "value", buyer);
        var expected = new TradeResult(TradeError.None, 1000, balance);
        Assert.Multiple(() =>
        {
            Assert.That(trade, Is.EqualTo(expected));
            Assert.That(session.TransferValue(id, "value", buyer), Is.EqualTo(expected));
            Assert.That(session.TransferValue(Guid.NewGuid(), "value", buyer).Error, Is.EqualTo(TradeError.AlreadyTransferred));
            Assert.That(session.TransferValue(id, "other", buyer).Error, Is.EqualTo(TradeError.IdConflict));
            Assert.That(session.GetBank().Balance, Is.EqualTo(1673 + balance));
            Assert.That(Balance(buyer), Is.EqualTo(multiplier * 10));
            Assert.That(random.Calls, Is.EqualTo(1));
            Assert.That(Query("SELECT multiplier FROM value_sales;"), Is.EqualTo(multiplier));
            Assert.That(Query("SELECT owned FROM registered_values;"), Is.EqualTo(kind == ValueKind.Document ? 1 : 0));
            Assert.That(session.OpenConversation(buyer).Any(message => message.Id.StartsWith(buyer + "-partial:")), Is.EqualTo(balance < 1000));
            if (documentId != null)
            {
                Assert.That(session.Documents.Get(documentId), Is.Not.Null);
                Assert.That(session.GetDocumentOffer(documentId), Is.Null);
            }
        });
    }

    [Test]
    public void DeliveryAndTradeOverflowLeaveOwnershipAndBothWalletsIntact()
    {
        var session = Started();
        _clock.Move(11);
        session.AdvanceWorld();
        session.RegisterValue("item", ValueKind.PhysicalItem, 1000);
        Assert.That(session.TransferValue(Guid.NewGuid(), "item", "ivan").Error, Is.EqualTo(TradeError.DeliveryRequired));
        AddDocument(session);
        session.RegisterValue("large", ValueKind.Document, long.MaxValue, "evidence");
        Assert.Throws<OverflowException>(() => session.TransferValue(Guid.NewGuid(), "large", "journalist"));
        Assert.Multiple(() =>
        {
            Assert.That(Query("SELECT COUNT(*) FROM value_sales;"), Is.Zero);
            Assert.That(Query("SELECT SUM(owned) FROM registered_values;"), Is.EqualTo(2));
            Assert.That(session.GetBank().Balance, Is.EqualTo(1673));
            Assert.That(Balance("journalist"), Is.EqualTo(900));
        });
    }

    [Test]
    public void VersionThreePreservesExistingDataAndAddsTheInvoiceAsANewMessage()
    {
        var legacy = Open(LocalSessionTests.LegacyScenario());
        legacy.CreateAccount("Старый профиль", "test-password");
        legacy.SetAvatar([1, 2, 3]);
        var intro = legacy.OpenConversation("unknown").Single();
        legacy.SelectReply(intro.Id, "received");
        var oldDocument = legacy.Documents.AddFromAttachment(intro.Attachments.Single().EventId)!;
        legacy.Authorize("n910vt", "fox_operator", "N910VT-relay");
        Sql("INSERT INTO expeditions(world_id, revision, payload) VALUES('legacy', 5, X'1234');");
        using (var connection = Connect())
            LocalSessionTests.RemoveWorldSchema(connection);

        var session = Open();
        Assert.That(session.CheckPassword("test-password"), Is.EqualTo(PasswordResult.Accepted));
        session.EnterWorkspace();
        session.EnterWorkspace();
        _clock.MoveSeconds(6);
        session.AdvanceWorld();
        var messages = session.OpenConversation("unknown");
        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Count.EqualTo(4));
            Assert.That(messages[0].Text, Is.EqualTo(intro.Text));
            Assert.That(messages[0].Replies, Is.Empty);
            Assert.That(messages[2].Replies, Has.Count.EqualTo(3));
            Assert.That(session.GetProfile(), Has.Property(nameof(PlayerProfile.Nickname)).EqualTo("Старый профиль"));
            Assert.That(session.GetProfile().Avatar, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(session.Documents.Get(oldDocument.Info.EventId)!.Content, Is.EqualTo(oldDocument.Content));
            Assert.That(session.GetDevices().Single().Authorized, Is.True);
            Assert.That(Query("SELECT revision FROM expeditions WHERE world_id = 'legacy';"), Is.EqualTo(5));
            Assert.That(session.GetBank().Requests, Has.Count.EqualTo(1));
        });
    }

    private static RequestStatus Status(LocalSession session, string contact) =>
        session.GetBank().Requests.First(request => !request.Incoming && request.ContactId == contact).Status;

    private LocalSession Open(Scenario? scenario = null, Random? random = null, IPhysicalDelivery? delivery = null)
    {
        var session = new LocalSession(_database, scenario ?? Scenario.LoadDefault(), _clock, random, delivery);
        session.Initialize();
        return session;
    }

    private LocalSession Started(Random? random = null, IPhysicalDelivery? delivery = null)
    {
        var session = Open(random: random, delivery: delivery);
        Enter(session);
        return session;
    }

    private static void Enter(LocalSession session)
    {
        session.CreateAccount("TestPilot", "test-password");
        session.EnterWorkspace();
    }

    private static void AddDocument(LocalSession session) =>
        session.Documents.Add(new ReceivedDocument("evidence", "Документ", "evidence.txt", DocumentFormat.Text, "Тест", [1]));

    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _database, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private void Sql(string sql)
    {
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long Query(string sql)
    {
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private long Balance(string contact) => Query($"SELECT balance FROM npc_wallets WHERE contact_id = '{contact}';");

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Move(double minutes) => _now = _now.AddMinutes(minutes);

        public void MoveSeconds(int seconds) => _now = _now.AddSeconds(seconds);
    }

    private sealed class EdgeRandom(bool useMaximum) : Random
    {
        public int Calls { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            Calls++;
            return useMaximum ? maxValue - 1 : minValue;
        }
    }

    private sealed class Delivered : IPhysicalDelivery
    {
        public bool IsDelivered(string valueId, string buyerId) => true;
    }
}
