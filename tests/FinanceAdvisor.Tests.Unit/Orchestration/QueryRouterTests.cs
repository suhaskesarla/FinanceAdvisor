namespace FinanceAdvisor.Tests.Unit.Orchestration;

using System.Diagnostics.Metrics;
using FinanceAdvisor.Core.Enums;
using FinanceAdvisor.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class QueryRouterTests
{
    // Minimal IMeterFactory that creates real Meters so Counter<T> works without side effects.
    private sealed class NullMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }

    private readonly QueryRouter _sut = new(NullLogger<QueryRouter>.Instance, new NullMeterFactory());

    // ── Layer 1: exact commands ───────────────────────────────────────────────

    [Theory]
    [InlineData("/balance")]
    [InlineData("/holdings")]
    [InlineData("/portfolio")]
    [InlineData("/BALANCE")]
    [InlineData("  /portfolio ")]
    public void GivenExactPortfolioCommand_WhenRouted_ThenReturnsPortfolio(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Portfolio);
    }

    [Theory]
    [InlineData("/brief")]
    [InlineData("/briefing")]
    [InlineData("/start")]
    public void GivenExactBriefingCommand_WhenRouted_ThenReturnsBriefing(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Briefing);
    }

    [Theory]
    [InlineData("/login")]
    public void GivenExactLoginCommand_WhenRouted_ThenReturnsZerodhaLogin(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.ZerodhaLogin);
    }

    [Theory]
    [InlineData("/help")]
    public void GivenExactHelpCommand_WhenRouted_ThenReturnsHelp(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Help);
    }

    // ── Layer 2: question detection (forces DeepPath) ─────────────────────────

    [Theory]
    [InlineData("What are my holdings?")]       // '?' + "what" prefix
    [InlineData("What is a mutual fund?")]      // analytical finance question
    [InlineData("Should I buy more HDFC?")]     // "should" prefix
    [InlineData("How is the market today?")]    // "how" prefix
    [InlineData("Why did NIFTY drop?")]         // "why" prefix
    [InlineData("Can I afford this stock?")]    // "can" prefix
    [InlineData("Is HDFC a good stock")]        // "is" prefix — no '?' needed
    [InlineData("Are markets open today")]      // "are" prefix
    [InlineData("Do I have enough balance")]    // "do" prefix
    [InlineData("Does NIFTY usually recover")]  // "does" prefix
    [InlineData("Explain P/E ratio")]           // imperative analytical verb
    [InlineData("any news today?")]             // contains '?'
    public void GivenQuestion_WhenRouted_ThenReturnsDeepPath(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.DeepPath);
    }

    // ── Layer 3a: phrase matching (action verb + keyword, any length) ─────────

    [Theory]
    [InlineData("show me my portfolio performance")]  // 5 words — exceeds keyword-match limit
    [InlineData("check my current balance today")]    // action + "balance"
    [InlineData("view my holdings right now")]        // action + "holdings"
    public void GivenActionPhraseWithPortfolioKeyword_WhenRouted_ThenReturnsPortfolio(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Portfolio);
    }

    [Theory]
    [InlineData("show me the latest news")]     // action + "news"
    [InlineData("give me a market update")]     // action + "market"
    public void GivenActionPhraseWithBriefingKeyword_WhenRouted_ThenReturnsBriefing(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Briefing);
    }

    // ── Layer 3b: short inputs with strong keyword match → shallow routes ─────

    [Theory]
    [InlineData("show my portfolio")]       // "portfolio" keyword, 3 words
    [InlineData("check my balance please")] // "balance" keyword, 4 words
    public void GivenShortNaturalLanguagePortfolio_WhenRouted_ThenReturnsPortfolio(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Portfolio);
    }

    [Theory]
    [InlineData("give me a brief")]     // "brief" keyword, 4 words
    [InlineData("market update please")] // "market" keyword, 3 words
    public void GivenShortNaturalLanguageBriefing_WhenRouted_ThenReturnsBriefing(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Briefing);
    }

    [Theory]
    [InlineData("I want to login")]    // "login" keyword, 4 words
    [InlineData("connect my account")] // "connect" keyword, 3 words
    public void GivenShortNaturalLanguageLogin_WhenRouted_ThenReturnsZerodhaLogin(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.ZerodhaLogin);
    }

    // ── Layer 4: AI-worthy — long inputs without a question marker ────────────

    [Fact]
    public void GivenInputLongerThanAiWorthyThreshold_WhenRouted_ThenReturnsDeepPath()
    {
        // 101 chars, no '?', no question starter, no keyword — length alone justifies AI
        string longInput = new string('a', 101);

        QueryRoute result = _sut.Route(longInput);

        result.Should().Be(QueryRoute.DeepPath);
    }

    // ── Layer 5: cheap fallback ───────────────────────────────────────────────

    [Theory]
    [InlineData("hello")]   // greeting — no keyword, no question, short
    [InlineData("thanks")]  // acknowledgement
    [InlineData("ok")]      // single-word non-command
    public void GivenShortUnrecognisedInput_WhenRouted_ThenReturnsHelp(string input)
    {
        // Returning Help rather than Briefing avoids silently assuming the user
        // wants market news when they typed a casual word.
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Help);
    }

    // ── Guard clauses ─────────────────────────────────────────────────────────

    [Fact]
    public void GivenEmptyString_WhenRouted_ThenThrowsArgumentException()
    {
        Action act = () => _sut.Route(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GivenNullString_WhenRouted_ThenThrowsArgumentException()
    {
        Action act = () => _sut.Route(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GivenWhitespaceOnly_WhenRouted_ThenThrowsArgumentException()
    {
        Action act = () => _sut.Route("   ");

        act.Should().Throw<ArgumentException>();
    }
}
