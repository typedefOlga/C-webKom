namespace CoreLibrary;

public sealed record GaOptions(
    int PopulationSize,
    int MaxGenerations,
    int TournamentSize,
    double CrossoverProbability,
    double MutationProbability,
    int EliteCount,
    int Seed
);