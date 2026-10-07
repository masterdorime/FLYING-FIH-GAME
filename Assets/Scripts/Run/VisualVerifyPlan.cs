using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // W0: fixed bookmark table for the headless capture harness
    // (Assets/Editor/VisualVerify.cs). Pure data in Runtime so
    // EditMode tests can pin it: every realm covered, a ring
    // close-up present, names unique (they become PNG filenames).
    // Seeds are fixed so captures are reproducible frame to frame.
    public static class VisualVerifyPlan
    {
        public enum Subject
        {
            Overview,
            FirstRing,
            FirstArch,
            FirstIsland,
            FirstSpire,
        }

        public struct Bookmark
        {
            public string Name;
            public string Spec;
            public int Seed;
            public Subject Subject;
        }

        public static readonly Bookmark[] Bookmarks = new Bookmark[]
        {
            new Bookmark { Name = "gauntlet-overview", Spec = "Gauntlet", Seed = 7, Subject = Subject.Overview },
            new Bookmark { Name = "gauntlet-arch", Spec = "Gauntlet", Seed = 7, Subject = Subject.FirstArch },
            new Bookmark { Name = "storm-island", Spec = "Storm", Seed = 11, Subject = Subject.FirstIsland },
            new Bookmark { Name = "storm-spire", Spec = "Storm", Seed = 11, Subject = Subject.FirstSpire },
            new Bookmark { Name = "sky-spire", Spec = "Sky", Seed = 13, Subject = Subject.FirstSpire },
            new Bookmark { Name = "lagoon-ring", Spec = "Lagoon", Seed = 5, Subject = Subject.FirstRing },
        };
    }
}
