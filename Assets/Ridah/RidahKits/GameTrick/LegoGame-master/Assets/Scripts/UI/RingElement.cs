
using UnityEngine;
using UnityEngine.Events;
using GameplayIngredients;
using GameplayIngredients.Events;

[CreateAssetMenu(fileName = "RingElement", menuName = "RingMenu/Element", order = 2)]
public class RingElement : EventBase
{
    public string Name;
    public Sprite Icon;
    public Ring NextRing;
    public bool trigger = true;
    public Callable[] events;
}
