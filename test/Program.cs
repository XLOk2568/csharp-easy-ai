// See https://aka.ms/new-console-template for more information
Console.WriteLine("请输测试内容:");
string? chooseTest_ = Console.ReadLine();   // 等待用户输入一行文本，允许为 null
switch (chooseTest_)
{
    case "sort":
        {
            List<float> scoreList= (0.5,23.1,9.2,56.);

            for (i = 0; i < scoreList.Count() - 1; i++)
            {
                for (int j = 0; j < scoreList.Count() - i - 1; j++)
                {
                    if (scoreList[j] > scoreList[j + 1])
                    {
                        temp = scoreList[j];
                        scoreList[j] = scoreList[j + 1];
                        scoreList[j + 1] = temp;
                        // 交换结果数组
                        temprj[0] = rj[j];
                        rj[j] = rj[j + 1];
                        rj[j + 1] = temprj[0];
                    }
                    else if (scoreList[j] < scoreList[j + 1])
                    {
                        stopNumber++;
                        if (stopNumber == scoreList.Count() - 1)
                        {
                            i = scoreList.Count() + 2; // 跳出外层循环
                        }
                    }
                }
            }
            // 获取最后一个组
            int[] lastGroup = temprj[temprj.Length - 1];
        }
        break;
        }
}
Console.WriteLine("Hello, World!");
