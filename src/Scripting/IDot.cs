namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface IDot
{
    [Description("Makes Dot say a custom text line")]
    LongRunningAction Say(string line, bool nearGomez, bool hideAfter);

    [Description("Hides Dot in Gomez's hat")]
    LongRunningAction ComeBackAndHide(bool withCamera);

    [Description("Spiral around the level, yo")]
    LongRunningAction SpiralAround(bool withCamera, bool hideDot);
}