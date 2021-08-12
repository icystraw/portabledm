namespace partialdownloadgui.Components
{
    public class ScramblerFunction
    {
        private string functionVariable;
        private string functionName;
        private int parameter;
        private ScramblerType type;

        public string FunctionVariable { get => functionVariable; set => functionVariable = value; }
        public string FunctionName { get => functionName; set => functionName = value; }
        public int Parameter { get => parameter; set => parameter = value; }
        public ScramblerType Type { get => type; set => type = value; }
    }
}
