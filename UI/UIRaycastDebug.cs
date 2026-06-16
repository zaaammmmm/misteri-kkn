using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIRaycastDebug : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData data =
                new PointerEventData(EventSystem.current);

            data.position = Input.mousePosition;

            List<RaycastResult> results =
                new List<RaycastResult>();

            EventSystem.current.RaycastAll(data, results);

            Debug.Log("=== UI HIT ===");

            foreach (var r in results)
                Debug.Log(r.gameObject.name);
        }
    }
}