using UnityEngine;

namespace KKN.Game.Inventory
{
    [CreateAssetMenu(fileName = "NewDocument", menuName = "KKN/Document")]
    public class DocumentData : ScriptableObject
    {
        public string documentID;
        public string title;

        [TextArea(5,10)]
        public string description;

        public Sprite icon;
        public Sprite fullImage;

        // Step 8: Track if document has been picked/read
        [HideInInspector]
        public bool isPicked = false;
    }
}
