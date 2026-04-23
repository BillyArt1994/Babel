namespace Babel
{
    public interface IEnemyAbility
    {
        void Init(Enemy owner, EnemyData data);
        void Tick(float deltaTime);
        void OnRemoved();
    }
}
