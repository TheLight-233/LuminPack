# 固定 wire 测试夹具

这些二进制记录是纳入版本控制的格式参考。构建后对本目录运行 wire harness 的 `verify`：读取每条记录、重新写入并比较完整字节。CI 不得在验证前覆盖基线。

模型和构造方式位于上级目录的 `WireFixtures.cs`。依赖布局的记录仅使用显式稳定值。修改协议须另行审查；普通回读成功不能证明与已有数据兼容。

`union-variable-utf16.bin` 和 `union-variable-utf8.bin` 覆盖混合长度 ATPD，包括 raw UTF-16 字符串、原始数值数组和带 null 的定长对象列表。UTF-16 字符串在所有目标上都使用偶数 raw 长度头，包括保留普通 Union 分派的平台。这些文件补充原有基线，不替换其他记录。
