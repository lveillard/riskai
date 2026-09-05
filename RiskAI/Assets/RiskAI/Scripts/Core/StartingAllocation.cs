using System;

namespace RiskAI.Core
{
    /// <summary>How the prototype chooses starting ownership.</summary>
    public enum StartingAllocationMode
    {
        /// <summary>Give each player one complete country; remaining countries are neutral.</summary>
        WholeCountries,

        /// <summary>Shuffle individual cities and distribute them evenly between players.</summary>
        IndividualCities
    }

    /// <summary>Pure result of a seeded starting allocation.</summary>
    public sealed class StartingAllocationPlan
    {
        /// <summary>Owner value used for unassigned cities/countries.</summary>
        public const int NeutralOwner = -1;

        /// <summary>Owner value used for a country split by IndividualCities mode.</summary>
        public const int MixedOwner = -2;

        public StartingAllocationPlan(int playerCount, int[] cityOwners, int[] countryOwners)
        {
            PlayerCount = playerCount;
            CityOwners = cityOwners;
            CountryOwners = countryOwners;
        }

        public int PlayerCount { get; private set; }

        /// <summary>Owner by city index. Values are player indices, NeutralOwner, or MixedOwner.</summary>
        public int[] CityOwners { get; private set; }

        /// <summary>Owner by country index. MixedOwner means that its cities were split.</summary>
        public int[] CountryOwners { get; private set; }

        public int CountCitiesForPlayer(int player)
        {
            if (player < 0 || player >= PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
            var count = 0;
            for (var i = 0; i < CityOwners.Length; i++)
                if (CityOwners[i] == player) count++;
            return count;
        }
    }

    /// <summary>
    /// Deterministic, engine-independent adaptation of the map's random base assignment.
    /// City indices are zero-based; cityCountryIds[city] names that city's country index.
    /// </summary>
    public static class StartingAllocation
    {
        public const int PrototypeCityCount = 12;
        public const int PrototypeCountryCount = 6;
        public const int PrototypePlayerCount = 2;

        private const uint NonZeroSeed = 0x9E3779B9u;

        /// <summary>Builds the 12-city/6-country/2-player prototype topology.</summary>
        public static StartingAllocationPlan GeneratePrototype(
            int seed,
            StartingAllocationMode mode = StartingAllocationMode.WholeCountries)
        {
            var cityCountryIds = new int[PrototypeCityCount];
            for (var city = 0; city < cityCountryIds.Length; city++)
                cityCountryIds[city] = city / 2;
            return Generate(seed, cityCountryIds, PrototypePlayerCount, mode);
        }

        /// <summary>
        /// Allocates the supplied topology. WholeCountries gives one distinct complete country
        /// to each player. IndividualCities gives each player an equal city count when possible.
        /// </summary>
        public static StartingAllocationPlan Generate(
            int seed,
            int[] cityCountryIds,
            int playerCount = PrototypePlayerCount,
            StartingAllocationMode mode = StartingAllocationMode.WholeCountries)
        {
            if (cityCountryIds == null) throw new ArgumentNullException(nameof(cityCountryIds));
            if (cityCountryIds.Length == 0) throw new ArgumentException("At least one city is required.", nameof(cityCountryIds));
            if (playerCount < 1) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (!Enum.IsDefined(typeof(StartingAllocationMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));

            var countryCount = 0;
            for (var city = 0; city < cityCountryIds.Length; city++)
            {
                var country = cityCountryIds[city];
                if (country < 0) throw new ArgumentOutOfRangeException(nameof(cityCountryIds));
                countryCount = Math.Max(countryCount, country + 1);
            }

            var countryHasCity = new bool[countryCount];
            for (var city = 0; city < cityCountryIds.Length; city++) countryHasCity[cityCountryIds[city]] = true;
            for (var country = 0; country < countryCount; country++)
                if (!countryHasCity[country]) throw new ArgumentException("Country indices must be contiguous.", nameof(cityCountryIds));
            if (playerCount > countryCount && mode == StartingAllocationMode.WholeCountries)
                throw new ArgumentException("WholeCountries needs at least one country per player.", nameof(playerCount));

            var cityOwners = new int[cityCountryIds.Length];
            var countryOwners = new int[countryCount];
            Fill(cityOwners, StartingAllocationPlan.NeutralOwner);
            Fill(countryOwners, StartingAllocationPlan.NeutralOwner);
            var randomState = unchecked((uint)seed);
            if (randomState == 0) randomState = NonZeroSeed;

            if (mode == StartingAllocationMode.WholeCountries)
            {
                var countries = new int[countryCount];
                for (var country = 0; country < countryCount; country++) countries[country] = country;
                Shuffle(countries, ref randomState);
                for (var player = 0; player < playerCount; player++)
                    countryOwners[countries[player]] = player;
                for (var city = 0; city < cityOwners.Length; city++)
                    cityOwners[city] = countryOwners[cityCountryIds[city]];
            }
            else
            {
                var cities = new int[cityCountryIds.Length];
                for (var city = 0; city < cities.Length; city++) cities[city] = city;
                Shuffle(cities, ref randomState);
                // Match the map's integer division: only complete player rounds are assigned.
                // Any remainder stays NeutralOwner for the caller to place or leave neutral.
                var assignedCityCount = (cities.Length / playerCount) * playerCount;
                for (var rank = 0; rank < assignedCityCount; rank++)
                    cityOwners[cities[rank]] = rank % playerCount;
                for (var country = 0; country < countryCount; country++)
                {
                    var owner = cityOwners[FindFirstCity(cityCountryIds, country)];
                    for (var city = 0; city < cityOwners.Length; city++)
                        if (cityCountryIds[city] == country && cityOwners[city] != owner)
                            owner = StartingAllocationPlan.MixedOwner;
                    countryOwners[country] = owner;
                }
            }

            return new StartingAllocationPlan(playerCount, cityOwners, countryOwners);
        }

        private static int FindFirstCity(int[] cityCountryIds, int country)
        {
            for (var city = 0; city < cityCountryIds.Length; city++)
                if (cityCountryIds[city] == country) return city;
            throw new InvalidOperationException("Country has no city.");
        }

        private static void Fill(int[] values, int value)
        {
            for (var i = 0; i < values.Length; i++) values[i] = value;
        }

        private static void Shuffle(int[] values, ref uint state)
        {
            for (var i = values.Length - 1; i > 0; i--)
            {
                var random = Next(ref state);
                var swap = (int)(random % (uint)(i + 1));
                var item = values[i];
                values[i] = values[swap];
                values[swap] = item;
            }
        }

        // Fixed unsigned LCG keeps results stable across Unity/.NET runtime versions.
        private static uint Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return state;
        }
    }
}
