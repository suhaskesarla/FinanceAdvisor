namespace FinanceAdvisor.Tests.Unit.Orchestration;

using FinanceAdvisor.Core.Enums;
using FinanceAdvisor.Services;
using FluentAssertions;
using Xunit;

public class QueryRouterTests
{
    private readonly QueryRouter _sut = new();

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

    [Theory]
    [InlineData("show my portfolio")]
    [InlineData("What are my holdings?")]
    [InlineData("check my balance please")]
    public void GivenNaturalLanguagePortfolio_WhenRouted_ThenReturnsPortfolio(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Portfolio);
    }

    [Theory]
    [InlineData("give me a brief")]
    [InlineData("market update please")]
    [InlineData("any news today?")]
    public void GivenNaturalLanguageBriefing_WhenRouted_ThenReturnsBriefing(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.Briefing);
    }

    [Theory]
    [InlineData("I want to login")]
    [InlineData("connect my account")]
    public void GivenNaturalLanguageLogin_WhenRouted_ThenReturnsZerodhaLogin(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.ZerodhaLogin);
    }

    [Theory]
    [InlineData("What is a mutual fund?")]
    [InlineData("Should I buy more HDFC?")]
    [InlineData("Explain P/E ratio")]
    public void GivenComplexQuery_WhenRouted_ThenReturnsDeepPath(string input)
    {
        QueryRoute result = _sut.Route(input);

        result.Should().Be(QueryRoute.DeepPath);
    }

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
