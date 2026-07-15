using SISLAB.Modules.Identity.Domain.Invitations;

namespace SISLAB.Modules.Identity.Tests.Domain.Invitations;

/// <summary>
/// Unit tests for the <see cref="InvitationToken"/> value object (card [E12] #75c): generation, SHA-256 hashing,
/// constant-time matching and structural equality — the mechanics that keep the raw token out of storage.
/// </summary>
public sealed class InvitationTokenTests
{
    [Fact]
    public void Generate_ProducesRawTokenAndMatchingHash()
    {
        (string rawToken, InvitationToken token) = InvitationToken.Generate();

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.False(string.IsNullOrWhiteSpace(token.TokenHash));
        Assert.NotEqual(rawToken, token.TokenHash); // the stored value is the hash, not the secret
        Assert.True(token.Matches(rawToken));
    }

    [Fact]
    public void Generate_YieldsAUniqueTokenEachCall()
    {
        (string first, _) = InvitationToken.Generate();
        (string second, _) = InvitationToken.Generate();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FromRawToken_IsDeterministic_SameRawSameHash()
    {
        (string rawToken, InvitationToken original) = InvitationToken.Generate();

        InvitationToken rehashed = InvitationToken.FromRawToken(rawToken);

        Assert.Equal(original.TokenHash, rehashed.TokenHash);
        Assert.Equal(original, rehashed); // structural equality by hash
    }

    [Fact]
    public void Matches_RejectsAWrongToken_AndEmptyInput()
    {
        (_, InvitationToken token) = InvitationToken.Generate();

        Assert.False(token.Matches("wrong-token"));
        Assert.False(token.Matches(""));
        Assert.False(token.Matches("   "));
    }

    [Fact]
    public void FromHash_RoundTripsThroughStoredHash()
    {
        (string rawToken, InvitationToken original) = InvitationToken.Generate();

        InvitationToken rehydrated = InvitationToken.FromHash(original.TokenHash);

        Assert.Equal(original, rehydrated);
        Assert.True(rehydrated.Matches(rawToken));
    }
}
