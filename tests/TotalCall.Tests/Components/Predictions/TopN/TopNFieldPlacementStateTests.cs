using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNFieldPlacementStateTests
{
    [Fact]
    public void Resolve_WhenQuestionIsMissing_ReturnsEmptyState()
    {
        var state = TopNFieldPlacementState.Resolve(
            CreateCompetition(),
            activeQuestion: null,
            storedPlacements: [],
            scoredPositionsCount: 3);

        Assert.Empty(state.QuestionAthletes);
        Assert.Empty(state.EligibleAthletes);
        Assert.Empty(state.FieldPlacements);
    }

    [Fact]
    public void Resolve_NormalizesStoredPlacementsAndAutoAddsEligibleAthletes()
    {
        var state = TopNFieldPlacementState.Resolve(
            CreateCompetition(),
            CreateQuestion(),
            [
                Pick("unknown-athlete", 1, totalKg: 999),
                Pick("athlete-b", 2, totalKg: 405),
                Pick("athlete-a", 3, totalKg: 305),
                Pick("athlete-b", 4, totalKg: 410)
            ],
            scoredPositionsCount: 1);

        Assert.Equal(["athlete-b", "athlete-c", "athlete-a"], state.QuestionAthletes.Select(athlete => athlete.Id));
        Assert.Equal(["athlete-b", "athlete-a"], state.EligibleAthletes.Select(athlete => athlete.Id));

        Assert.Collection(
            state.FieldPlacements,
            first =>
            {
                Assert.Equal(1, first.Position);
                Assert.Equal("athlete-b", first.AthleteId);
                Assert.Equal(405m, first.PredictedTotalKg);
                Assert.True(first.IsScored);
                Assert.False(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Equal(2, second.Position);
                Assert.Equal("athlete-a", second.AthleteId);
                Assert.Equal(305m, second.PredictedTotalKg);
                Assert.False(second.IsScored);
                Assert.False(second.IsAutoSeeded);
            });
    }

    [Fact]
    public void Resolve_DoesNotAutoAddWithdrawnAthletesButPreservesStoredWithdrawnPick()
    {
        var state = TopNFieldPlacementState.Resolve(
            CreateCompetition(),
            CreateQuestion(),
            [Pick("athlete-c", 1, totalKg: 390)],
            scoredPositionsCount: 2);

        Assert.Collection(
            state.FieldPlacements,
            first =>
            {
                Assert.Equal("athlete-c", first.AthleteId);
                Assert.Equal(1, first.Position);
                Assert.True(first.IsScored);
                Assert.False(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Equal("athlete-b", second.AthleteId);
                Assert.Equal(2, second.Position);
                Assert.True(second.IsScored);
                Assert.True(second.IsAutoSeeded);
            },
            third =>
            {
                Assert.Equal("athlete-a", third.AthleteId);
                Assert.Equal(3, third.Position);
                Assert.False(third.IsScored);
                Assert.True(third.IsAutoSeeded);
            });
    }

    [Fact]
    public void BuildDefaultFieldPlacements_UsesEligibleAthletesOnlyInSeedOrder()
    {
        var state = TopNFieldPlacementState.Resolve(
            CreateCompetition(),
            CreateQuestion(),
            storedPlacements: [],
            scoredPositionsCount: 1);

        var defaults = state.BuildDefaultFieldPlacements();

        Assert.Collection(
            defaults,
            first =>
            {
                Assert.Equal("athlete-b", first.AthleteId);
                Assert.Equal(1, first.Position);
                Assert.Equal(400m, first.PredictedTotalKg);
                Assert.True(first.IsScored);
                Assert.True(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Equal("athlete-a", second.AthleteId);
                Assert.Equal(2, second.Position);
                Assert.Equal(300m, second.PredictedTotalKg);
                Assert.False(second.IsScored);
                Assert.True(second.IsAutoSeeded);
            });
    }

    [Fact]
    public void NormalizeForAnswer_RemovesUnknownBlankAndDuplicateAthletes()
    {
        var state = TopNFieldPlacementState.Resolve(
            CreateCompetition(),
            CreateQuestion(),
            storedPlacements: [],
            scoredPositionsCount: 1);

        var normalized = state.NormalizeForAnswer(
            [
                Pick("athlete-a", 3, totalKg: 305),
                Pick("", 1, totalKg: 999),
                Pick("unknown-athlete", 2, totalKg: 999),
                Pick("athlete-a", 4, totalKg: 310),
                Pick("athlete-b", 5, totalKg: 405)
            ]);

        Assert.Collection(
            normalized,
            first =>
            {
                Assert.Equal("athlete-a", first.AthleteId);
                Assert.Equal(1, first.Position);
                Assert.True(first.IsScored);
                Assert.Equal(305m, first.PredictedTotalKg);
            },
            second =>
            {
                Assert.Equal("athlete-b", second.AthleteId);
                Assert.Equal(2, second.Position);
                Assert.False(second.IsScored);
                Assert.Equal(405m, second.PredictedTotalKg);
            });
    }

    [Fact]
    public void HasSameField_ComparesStructuralPlacementFieldsOnly()
    {
        var stored = new[] { Pick("athlete-a", 1, totalKg: 300) };
        var normalized = new[] { Pick("ATHLETE-A", 1, totalKg: 320) };

        Assert.True(TopNFieldPlacementState.HasSameField(stored, normalized));
        Assert.False(TopNFieldPlacementState.HasSameField(stored, [Pick("athlete-a", 2, totalKg: 300)]));
    }

    private static Competition CreateCompetition()
    {
        return new Competition
        {
            Id = "worlds-2026",
            Slug = "worlds-2026",
            Name = "Worlds 2026",
            ConfigVersion = "1",
            Athletes =
            [
                CreateAthlete("athlete-a", "Beta", 300m),
                CreateAthlete("athlete-b", "Alpha", 400m),
                CreateAthlete("athlete-c", "Gamma", 400m, AthleteStatus.Withdrawn),
                CreateAthlete("athlete-d", "Delta", 200m)
            ]
        };
    }

    private static Athlete CreateAthlete(
        string id,
        string displayName,
        decimal seedTotalKg,
        AthleteStatus status = AthleteStatus.Active)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = displayName,
            SeedTotalKg = seedTotalKg,
            Status = status
        };
    }

    private static PredictionQuestion CreateQuestion()
    {
        return new PredictionQuestion
        {
            Id = "women-52",
            Type = PredictionQuestionType.CategoryPodium,
            Title = "Women 52",
            AthleteIds = ["athlete-a", "athlete-b", "athlete-c"]
        };
    }

    private static AthletePlacementPick Pick(string athleteId, int position, decimal totalKg)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedTotalKg = totalKg
        };
    }
}
