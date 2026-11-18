namespace ReactionTimeMeasurementApp.Domain;

// A mérés előkészítése során felmerülő, a felhasználónak szóló (nem programhiba jellegű) probléma,
// pl. a tekintet-ellenőrzéshez nincs beállítva a Tobii display area, vagy az inger beállított legkisebb
// távolsága a fixációs ponttól nem fér el a mérőfelületen.
public sealed class MeasurementSetupException : Exception
{
    public MeasurementSetupException(string message)
        : base(message)
    {
    }
}
