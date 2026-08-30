// Shared between ForcepsController (which forceps this is) and CottonState
// (which forceps currently holds this cotton, if any - null means unheld).
public enum ForcepsRole
{
    Pickup,
    Handling
}