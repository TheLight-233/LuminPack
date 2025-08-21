using LuminPack.Core;

namespace LuminPack.Interface;

public interface ILuminPackEvaluator<T>
{
    public void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T? value);
}