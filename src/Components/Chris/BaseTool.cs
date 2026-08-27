namespace FezEditor.Components.Chris;

internal abstract class BaseTool
{
    protected IChrisEditor Chris { get; }

    protected BaseTool(IChrisEditor chris)
    {
        Chris = chris;
    }

    public void Update()
    {
        TestConditions();
        if (IsToolAllowed(Chris.CurrentTool))
        {
            Act();
        }
    }

    public virtual void DrawOverlay()
    {
    }

    protected virtual void TestConditions()
    {
    }

    protected virtual void Act()
    {
    }

    protected abstract bool IsToolAllowed(ChrisTool tool);

    protected enum LmbState
    {
        Idle,
        Pressed,
        Dragging
    }
}