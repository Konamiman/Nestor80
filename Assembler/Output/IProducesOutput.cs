using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Output
{
    public interface IProducesOutput : IChangesLocationCounter
    {
        byte[] OutputBytes { get; set; }

        Tuple<int, IExpressionPart[]>[] Expressions { get; set; }
    }
}
