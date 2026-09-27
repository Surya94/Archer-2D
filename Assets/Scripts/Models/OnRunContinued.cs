using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dispatched when a run that had already ended is resumed (currently: after a rewarded-ad
/// revive granted fresh arrows). Bow listens for this to nock the next arrow.
///
/// Deliberately its own signal rather than reusing OnArrowsAdded: the streak bonus dispatches
/// OnAddArrows(1) mid-round, and between Shoot() and the arrow despawning Bow.newArrow is null,
/// so an OnArrowsAdded listener would spawn a second arrow while the first is still in flight.
/// </summary>
public class OnRunContinued
{
}
