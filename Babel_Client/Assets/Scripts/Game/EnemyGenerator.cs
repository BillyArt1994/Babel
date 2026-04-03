using UnityEngine;
using QFramework;

namespace Babel
{
	public partial class EnemyGenerator : ViewController
	{
		void Start()
		{
			// Code Here
		}

		void Update() 
		{
			Enemy.Instantiate()
				.Position(new Vector3(0, 0, 0))
				.Show();
        }
	}
}
