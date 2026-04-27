using UnityEngine;

[CreateAssetMenu(fileName = "NewAspect", menuName = "Grimoire/Aspect Data")]
public class AspectData : ScriptableObject
{
    public string aspectName;
    [TextArea(3, 10)]
    public string description;
    public Sprite aspectIcon;
    public bool isUnlocked = false;
}

