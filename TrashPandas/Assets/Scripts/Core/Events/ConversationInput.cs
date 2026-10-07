using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Core.Events
{
    /// <summary>During a conversation the mouse buttons mean "shake hands", not "grab whatever is near".</summary>
    public static class ConversationInput
    {
        public static SlotInput Filter(SlotInput input, bool inConversation)
        {
            if (!inConversation) return input;
            input.GrabOne = false;
            input.GrabBoth = false;
            return input;
        }
    }
}
