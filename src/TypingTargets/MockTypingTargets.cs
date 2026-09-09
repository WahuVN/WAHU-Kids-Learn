using System.Collections.Generic;

namespace WAHU.Typing.Targets
{
    public static class MockTypingTargets
    {
        public static IList<TypingTargetSpawnRequest> CreateTen()
        {
            return new List<TypingTargetSpawnRequest>
            {
                Request("vi_meo", "mèo", new[] { "mèo", "meo" }, "vi", 1, "rescue", 1, TypingTargetMovementKind.Satellite, 0),
                Request("vi_trang", "trăng", new[] { "trăng", "trang" }, "vi", 2, "unlock", 1, TypingTargetMovementKind.Ufo, 1),
                Request("vi_do", "đỏ", new[] { "đỏ", "do" }, "vi", 1, "shoot", 1, TypingTargetMovementKind.Asteroid, 2),
                Request("vi_ca", "cá", new[] { "cá", "ca" }, "vi", 2, "rescue", 1, TypingTargetMovementKind.Satellite, null),
                Request("vi_sao", "sao", new[] { "sao" }, "vi", 3, "shoot", 2, TypingTargetMovementKind.Asteroid, null),
                Request("en_moon", "moon", new[] { "moon" }, "en", 1, "unlock", 1, TypingTargetMovementKind.Ufo, null),
                Request("en_star", "star", new[] { "star" }, "en", 2, "shoot", 1, TypingTargetMovementKind.Asteroid, null),
                Request("en_ship", "ship", new[] { "ship" }, "en", 2, "rescue", 1, TypingTargetMovementKind.Satellite, null),
                Request("en_cat", "cat", new[] { "cat" }, "en", 2, "shoot", 1, TypingTargetMovementKind.Asteroid, null),
                Request("en_meteor_boss", "meteor", new[] { "meteor" }, "en", 4, "boss", 3, TypingTargetMovementKind.Ufo, 1)
            };
        }

        private static TypingTargetSpawnRequest Request(
            string id,
            string display,
            string[] inputs,
            string language,
            int difficulty,
            string kind,
            int reward,
            TypingTargetMovementKind movement,
            int? lane)
        {
            return new TypingTargetSpawnRequest
            {
                Target = new TypingTarget
                {
                    Id = id,
                    DisplayText = display,
                    AcceptedInputs = new List<string>(inputs),
                    Language = language,
                    Difficulty = difficulty,
                    Kind = kind,
                    RewardValue = reward
                },
                MovementKind = movement,
                PreferredLane = lane
            };
        }
    }
}
