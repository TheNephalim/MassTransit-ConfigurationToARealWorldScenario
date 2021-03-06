﻿using Automatonymous;
using Hangfire;
using PizzaApi.MessageContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassTransit.Logging;
using Newtonsoft.Json;

namespace PizzaApi.StateMachines
{
    public class OrderStateMachine : MassTransitStateMachine<Order>
    {
        public OrderStateMachine()
        {
            Logger.Get("mongoCustomLog").InfoFormat("OrderStateMachine ctor");

            InstanceState(x => x.CurrentState);

            Event(() => RegisterOrder,
                cc => cc.CorrelateBy(order => order.OrderID,
                                    context => context.Message.OrderID)
                        .SelectId(context => context.Message.CorrelationId));

            Event(() => ApproveOrder, cc => cc.CorrelateById(context => context.Message.CorrelationId));
            Event(() => CloseOrder, cc => cc.CorrelateById(context => context.Message.CorrelationId));
            Event(() => RejectOrder, cc => cc.CorrelateById(context => context.Message.CorrelationId));

            Initially(
                When(RegisterOrder)
                    .Then(context =>
                    {

                        //throw new ArgumentException("Test for monitoring sagas");

                        context.Instance.Created = context.Data.Timestamp;
                        context.Instance.OrderID = context.Data.OrderID;
                        context.Instance.CustomerName = context.Data.CustomerName;
                        context.Instance.CustomerPhone = context.Data.CustomerPhone;
                        context.Instance.PizzaID = context.Data.PizzaID;

                        Logger.Get("mongoCustomLog").InfoFormat("Register Order {0}", JsonConvert.SerializeObject(context.Instance));
                    })
                    .TransitionTo(Registered)
                    .Publish(context => new OrderRegisteredEvent(context.Instance))
                );

            During(Registered,
                When(ApproveOrder)
                    .Then(context =>
                    {
                        //throw new ArgumentException("Test for monitoring sagas");

                        context.Instance.Updated = context.Data.Timestamp;
                        context.Instance.EstimatedTime = context.Data.EstimatedTime;
                        context.Instance.Status = context.Data.Status;

                        var delayedTimeInSeconds = context.Instance.EstimatedTime.Value * 60 * 0.65f;
                        Console.WriteLine("delayedTime (in seconds): " + delayedTimeInSeconds);
                        BackgroundJob.Schedule(() => Console.WriteLine("Send notification to client: Pay attention please. Your order is near to be done!"),
                                                        TimeSpan.FromSeconds(delayedTimeInSeconds));

                        Logger.Get("mongoCustomLog").InfoFormat("Approve Order {0}", JsonConvert.SerializeObject(context.Instance));
                    })
                    .ThenAsync(async context =>
                    {
                        //throw new ArgumentException("Test for monitoring sagas");

                        await Console.Out.WriteLineAsync(string.Format("Send notification to client {0} with order id: {1} about your order status 'APPROVED'.",
                                                                                                context.Instance.CustomerName, context.Instance.OrderID));
                    })
                    .TransitionTo(Approved),
                //.Publish(context => new OrderApprovedEvent(context.Instance))//In this scenario, i don´t need of this event...
                When(RejectOrder)
                    .Then(context =>
                    {
                        context.Instance.Updated = context.Data.Timestamp;
                        context.Instance.RejectedReasonPhrase = context.Data.RejectedReasonPhrase;

                        Logger.Get("mongoCustomLog").InfoFormat("Reject Order {0}", JsonConvert.SerializeObject(context.Instance));
                    })
                    .ThenAsync(async context => await Console.Out.WriteLineAsync(string.Format("Send notification to client {0} with order id {1} about your order status 'REJECTED', reason: {2}.",
                                                                                                context.Instance.CustomerName, context.Instance.OrderID, context.Instance.RejectedReasonPhrase)))
                    .Finalize()
                );

            During(Approved,
                When(CloseOrder)
                    .Then(context =>
                    {
                        //throw new ArgumentException("Test for monitoring sagas");
                        context.Instance.Updated = context.Data.Timestamp;
                        context.Instance.Status = context.Data.Status;

                        Logger.Get("mongoCustomLog").InfoFormat("Close Order {0}", JsonConvert.SerializeObject(context.Instance));
                    })
                    .ThenAsync(async context => await Console.Out.WriteLineAsync(string.Format("Send notification to client {0} with order id: {1} about your order status 'CLOSED'",
                                                                                                context.Instance.CustomerName, context.Instance.OrderID)))
                    .Finalize()
                );

            SetCompletedWhenFinalized();
        }

        public State Registered { get; private set; }
        public State Approved { get; private set; }
        //Should add Closed state?
        public Event<IRegisterOrderCommand> RegisterOrder { get; private set; }
        public Event<IApproveOrderCommand> ApproveOrder { get; private set; }
        public Event<ICloseOrderCommand> CloseOrder { get; private set; }
        public Event<IRejectOrderCommand> RejectOrder { get; private set; }

    }
}
