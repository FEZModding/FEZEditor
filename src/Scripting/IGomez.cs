namespace FezEditor.Scripting;

[Entity(Static = true)]
internal interface IGomez
{
    [Description("The number of small golden cubes the player's collected")]
    int CollectedCubes { get; }

    [Description("Is he standing on solid ground?")]
    bool Grounded { get; }

    [Description("Is Gomez controllable by the player?")]
    bool CanControl { get; }

    [Description("Is Gomez visible?")]
    bool Visible { get; }

    bool IsOnLadder { get; }

    bool Alive { get; }

    int CollectedSplits { get; }

    event Action EnteredDoor;

    event Action Jumped;

    event Action ClimbedLadder;

    event Action ClimbedVine;

    event Action LookedAround;

    event Action LiftedObject;

    event Action ThrewObject;

    event Action OpenedMenuCube;

    event Action ReadSign;

    event Action GrabbedLedge;

    event Action DropObject;

    event Action DroppedLedge;

    event Action Hoisted;

    event Action ClimbedOverLadder;

    event Action DroppedFromLadder;

    event Action ReadMail;

    event Action CollectedSplitUpCube;

    event Action CollectedShard;

    event Action CollectedAnti;

    event Action CollectedGlobalAnti;

    event Action CollectedPieceOfHeart;

    event Action OpenedTreasure;

    event Action Landed;

    void OnEnterDoor();

    void OnJump();

    void OnClimbLadder();

    void OnClimbVine();

    void OnLookAround();

    void OnLiftObject();

    void OnThrowObject();

    void OnOpenMenuCube();

    void OnReadSign();

    void OnGrabLedge();

    void OnDropObject();

    void OnHoist();

    void OnDropLedge();

    void OnClimbOverLadder();

    void OnDropFromLadder();

    void OnReadMail();

    void OnCollectedSplitUpCube();

    void OnCollectedShard();

    void OnCollectedGlobalAnti();

    void OnCollectedAnti();

    void OnCollectedPieceOfHeart();

    void OnOpenTreasure();

    void OnLand();

    [Description("Sets whether Gomez can be controlled by the player")]
    void SetCanControl(bool controllable);

    [Description("Sets the current action (animation) for Gomez")]
    void SetAction(string actionName);

    [Description("Allows Gomez to enter that tunnel/passageway by pressing up")]
    LongRunningAction AllowEnterTunnel();

    [Description("Shows/Hides gomez's fez")]
    void SetFezVisible(bool visible);

    [Description("Shows/Hides gomez")]
    void SetGomezVisible(bool visible);
}