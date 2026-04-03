
using UnityEngine;
using QFramework;

// 1.请在菜单 编辑器扩展/Namespace Settings 里设置命名空间
// 2.命名空间更改后，生成代码之后，需要把逻辑代码文件（非 Designer）的命名空间手动更改
namespace Babel
{
    public partial class Enemy : ViewController
    {
        public float HP = 15;

        public float MovementSpeed = 2.0f;

        public Babel.Path path;

        private void Update()
        {
            if (HP > 0)
            {
                this.DestroyGameObjGracefully();
                Global.Exp.Value++;
            }

            var targetPos = new Vector3(path.wayPointList[0].transform.position.x, transform.position.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, MovementSpeed * Time.deltaTime);
            
        }

    }
}