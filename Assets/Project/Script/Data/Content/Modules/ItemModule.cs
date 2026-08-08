using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// One capability bolted onto an <see cref="ItemDefinition"/>: what the item can do,
    /// as data.
    /// </summary>
    /// <remarks>
    /// Composition rather than inheritance, because the content in GDD §8.3 does not form a
    /// tree. A laser pointer is a weapon <em>and</em> a light source, a fire extinguisher is
    /// a weapon <em>and</em> a utility, tape is neither. Subclassing forces a diamond the
    /// first time two of those meet; a list of modules does not.
    /// <para>
    /// A module holds <b>data only</b>. It is a ScriptableObject, so one asset is shared by
    /// every instance of the item — per-instance state such as the charge left in this
    /// particular flashlight cannot live here. Systems on the server read the numbers and
    /// keep the state.
    /// </para>
    /// </remarks>
    public abstract class ItemModule : ScriptableObject
    {
    }
}
