
using UnityEngine;
using QFramework;

// 1.请在菜单 编辑器扩展/Namespace Settings 里设置命名空间
// 2.命名空间更改后，生成代码之后，需要把逻辑代码文件（非 Designer）的命名空间手动更改
namespace Babel
{
    public partial class Enemy : ViewController, IDamageable
    {
        public float HP = 15;

        public float MovementSpeed = 2.0f;

        public int buildAbility = 25;

        public Babel.Path path;

        // IDamageable
        public Vector2 Position => (Vector2)transform.position;
        public bool IsAlive => HP > 0;

        public void TakeDamage(float damage, bool isCrit)
        {
            HP -= damage;
        }

        private void Update()
        {
            if (HP <= 0)
            {
                EnemyEvents.RaiseEnemyDied(Position);
                this.DestroyGameObjGracefully();
                Global.Exp.Value++;
                return;
            }

            if (path.IsCurrentLayerBuildCompleted() != true)
            {
                var curWayPointIndex = 0;
                var nearestDistance = float.MaxValue;
                for (int i = 0; i < path.wayPointList.Length; i++)
                {
                    var distance = Vector3.Distance(path.wayPointList[i].transform.position, transform.position);
                    if (distance <= nearestDistance && path.wayPointList[i].IsBilding == false && path.wayPointList[i].IsBuildCompleted == false)
                    {
                        nearestDistance = distance;
                        curWayPointIndex = i;
                    }
                }

                var targetPos = new Vector3(path.wayPointList[curWayPointIndex].transform.position.x, path.wayPointList[curWayPointIndex].transform.position.y - 0.5f, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);

                if ((transform.position - targetPos).magnitude <= 0.01)
                {
                    path.wayPointList[curWayPointIndex].AddBuildProgress(buildAbility);
                    path.wayPointList[curWayPointIndex].IsBilding = false;
                    this.DestroyGameObjGracefully();
                }

            }
            else
            {
                var targetPos = new Vector3(path.wayPointList[path.getGatewayIndex()].transform.position.x, transform.position.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);
                if ((transform.position - targetPos).magnitude <= 0.01)
                {
                    if (path.nextLayerPath == null)
                    {
                        UIKit.OpenPanel<UIGameOverPanel>();
                    }
                    else
                    {
                        path = path.nextLayerPath;
                    }

                }
            }
        }
    }
}