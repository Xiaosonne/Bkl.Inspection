using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace TestProject2
{
    public class ObserverTest
    {
        ITestOutputHelper output;
        public ObserverTest(ITestOutputHelper output)
        {
            this.output = output;
        }
        [Fact]
        public void TestObserverSubject()
        {
            Subject<int> sub = new Subject<int>();
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(10000);
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    sub.OnNext(1);
                    Thread.Sleep(1000);
                }
                sub.OnCompleted();
            });

            var lis = new List<int>();
            var pubstream = sub.Publish();
         
            var data = false;
            pubstream.Sum().Subscribe(s =>
            {
                data = true;
                Assert.True(s >= 9);
            });
            pubstream.Select(s => s * 2).Subscribe(t =>
            {
                lis.Add(t);
            });
            pubstream.Connect();
            int i = 0;
            Dictionary<int, int> dic = new Dictionary<int, int>();
            while (!cts.Token.IsCancellationRequested)
            {
                Thread.Sleep(1001);
                dic.Add(i++, lis.Count);
                this.output.WriteLine($"{i} {lis.Count}");
            }
            Assert.True(dic[0] > 0 || dic[1] > 0 || dic[2] > 0 || dic[4] > 0);
            Thread.Sleep(1001);
            Assert.True(data, "pubstream.Sum()");
        }
        [Fact]
        public void TestObserver()
        {
            var data = Observable.Create<int>((observer) =>
             {
                 Enumerable.Range(0, 9)
                 .Select(s => s)
                 .ToList()
                 .ForEach(s =>
                 {
                     observer.OnNext(s);
                 });
                 return new BooleanDisposable();
             }).Publish();


            var repeat = data.Repeat();
            int i = 1;
            repeat.Subscribe(data =>
            {
                Assert.True(data >= 0 && data <= 10);
                i++;
            });
            var repeatwhendata = data.RepeatWhen((seq) =>
                {
                    Subject<Unit> sub1 = new Subject<Unit>();
                    seq.Count().Subscribe(s =>
                    {
                        if (s == 20)
                        {
                            sub1.OnNext(Unit.Default);
                        }
                    });
                    return sub1.AsObservable();
                });
            repeatwhendata.Count().Subscribe(s =>
            {
                Assert.Equal(20, s);
            });

            Thread.Sleep(5000);
        }
    }
}
