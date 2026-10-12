using System.Security.Claims;
using Limpide.Core.Administration;

namespace Limpide.Core.Tests.Administration;

public class ActorTests
{
    private const string Microsoft = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";

    private static ClaimsPrincipal User(string issuer, params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value, ClaimValueTypes.String, issuer)), "oidc"));

    [Fact]
    public void The_identifier_is_the_issuer_and_subject_pair()
    {
        var actor = Actor.From(User(Microsoft, ("sub", "AbC123"), ("name", "Alexandre")))!;
        Assert.Equal($"{Microsoft}#AbC123", actor.Id);
        Assert.Equal("Alexandre", actor.Name);
    }

    [Fact]
    public void The_same_subject_at_another_provider_is_another_person()
    {
        var microsoft = Actor.From(User(Microsoft, ("sub", "AbC123")))!;
        var other = Actor.From(User("https://id.example.org", ("sub", "AbC123")))!;

        Assert.True(microsoft.IsIn([microsoft.Id]));
        Assert.False(other.IsIn([microsoft.Id]));
    }

    [Fact]
    public void The_allow_list_is_exact()
    {
        var actor = Actor.From(User(Microsoft, ("sub", "AbC123")))!;
        Assert.False(actor.IsIn([$"{Microsoft}#abc123"]));
        Assert.False(actor.IsIn([]));
    }

    [Fact]
    public void Without_a_subject_there_is_no_actor() =>
        Assert.Null(Actor.From(User(Microsoft, ("name", "Quelqu'un"))));

    [Fact]
    public void The_name_falls_back_to_the_username_then_the_subject()
    {
        Assert.Equal("a@exemple.fr", Actor.From(User(Microsoft, ("sub", "1"), ("preferred_username", "a@exemple.fr")))!.Name);
        Assert.Equal("1", Actor.From(User(Microsoft, ("sub", "1")))!.Name);
    }
}
