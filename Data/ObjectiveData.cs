using UnityEngine;

namespace KKN.Game.Data
{
    public enum ObjectiveType
    {
        FindItem,
        ReachLocation,
        UseItem,
        SolvePuzzle,
        RestorePower,
        Escape
    }

    /// <summary>
    /// ScriptableObject defining a single objective step.
    /// Enables varied progression beyond simple key hunting.
    /// </summary>
    [CreateAssetMenu(fileName = "ObjectiveData", menuName = "KKN/Objective Data")]
    public class ObjectiveData : ScriptableObject
    {
        [Tooltip("Type of objective")]
        public ObjectiveType type;

        [Tooltip("Display text shown to player")]
        [TextArea(2, 3)]
        public string description;

        [Tooltip("Hint shown when player is stuck")]
        [TextArea(2, 3)]
        public string hint;

        [Tooltip("Item required for UseItem objectives")]
        public ItemData requiredItem;

        [Tooltip("Target location tag for ReachLocation")]
        public string targetTag;

        [Tooltip("Auto-complete when condition met, or require manual confirmation")]
        public bool autoComplete = true;

        [Tooltip("Show ambiguity — don't reveal exact location")]
        public bool isAmbiguous;
    }
}

