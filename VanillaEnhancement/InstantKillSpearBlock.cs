// 秒杀测试矛: 继承 SpearBlock, 使用铁矛的纹理(槽位47手柄/63头部)和 Models/Spear 模型
// 攻击力 9999, 无投掷功能, 左键击中生物时秒杀并显示血量
namespace Game {
    public class InstantKillSpearBlock : SpearBlock {
        public InstantKillSpearBlock() : base(47, 63) { }
    }
}
