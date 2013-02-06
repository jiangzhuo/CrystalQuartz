using Quartz.Impl.Matchers;

namespace CrystalQuartz.Core
{
    using System;
    using System.Collections.Generic;
    using Domain;
    using Quartz;
    using SchedulerProviders;

    public class DefaultSchedulerDataProvider : ISchedulerDataProvider
    {
        private readonly ISchedulerProvider _schedulerProvider;

        public DefaultSchedulerDataProvider(ISchedulerProvider schedulerProvider)
        {
            _schedulerProvider = schedulerProvider;
        }

        public SchedulerData Data
        {
            get
            {
                var scheduler = _schedulerProvider.Scheduler;
                var metadata = scheduler.GetMetaData();
                return new SchedulerData
                           {
                               Name = scheduler.SchedulerName,
                               InstanceId = scheduler.SchedulerInstanceId,
                               JobGroups = GetJobGroups(scheduler),
                               TriggerGroups = GetTriggerGroups(scheduler),
                               Status = GetSchedulerStatus(scheduler),
                               IsRemote = metadata.SchedulerRemote,
                               JobsExecuted = metadata.NumberOfJobsExecuted,
                               RunningSince = metadata.RunningSince,
                               SchedulerType = metadata.SchedulerType
                           };
            }
        }

        public JobDetailsData GetJobDetailsData(JobKey jobKey)
        {
            var scheduler = _schedulerProvider.Scheduler;

            if (scheduler.IsShutdown)
            {
                return null;
            }

            var job = scheduler.GetJobDetail(jobKey);
            if (job == null)
            {
                return null;
            }

            var detailsData = new JobDetailsData
            {
                PrimaryData = GetJobData(scheduler, jobKey)
            };

            foreach (var key in job.JobDataMap.Keys)
            {
                detailsData.JobDataMap.Add(key, job.JobDataMap[key]);
            }

            detailsData.JobProperties.Add("Description", job.Description);
            detailsData.JobProperties.Add("Full name", job.Key.Name);
            detailsData.JobProperties.Add("Job type", job.JobType);
            detailsData.JobProperties.Add("Durable", job.Durable);
            //detailsData.JobProperties.Add("Volatile", job.);

            return detailsData;
        }

        public IEnumerable<ActivityEvent> GetJobEvents(string name, DateTime minDateUtc, DateTime maxDateUtc)
        {
            return new List<ActivityEvent>();
        }

        public SchedulerStatus GetSchedulerStatus(IScheduler scheduler)
        {
            if (scheduler.IsShutdown)
            {
                return SchedulerStatus.Shutdown;
            }

            if (scheduler.GetJobGroupNames() == null || scheduler.GetJobGroupNames().Count== 0)
            {
                return SchedulerStatus.Empty;
            }

            if (scheduler.IsStarted)
            {
                return SchedulerStatus.Started;
            }

            return SchedulerStatus.Ready;
        }

        private static ActivityStatus GetTriggerStatus(TriggerKey triggerKey, IScheduler scheduler)
        {
            var state = scheduler.GetTriggerState(triggerKey);
            switch (state)
            {
                case TriggerState.Paused:
                    return ActivityStatus.Paused;
                case TriggerState.Complete:
                    return ActivityStatus.Complete;
                default:
                    return ActivityStatus.Active;
            }
        }

        private static ActivityStatus GetTriggerStatus(ITrigger trigger, IScheduler scheduler)
        {
            return GetTriggerStatus(trigger.Key, scheduler);
        }

        private static IList<TriggerGroupData> GetTriggerGroups(IScheduler scheduler)
        {
            var result = new List<TriggerGroupData>();
            if (!scheduler.IsShutdown)
            {
                foreach (var groupName in scheduler.GetTriggerGroupNames())
                {
                    var data = new TriggerGroupData(groupName);
                    data.Init();
                    result.Add(data);
                }
            }

            return result;
        }

        private static IList<JobGroupData> GetJobGroups(IScheduler scheduler)
        {
            var result = new List<JobGroupData>();

            if (!scheduler.IsShutdown)
            {
                foreach (var groupName in scheduler.GetJobGroupNames())
                {
                    var groupData = new JobGroupData(
                        groupName,
                        GetJobs(scheduler, groupName));
                    groupData.Init();
                    result.Add(groupData);
                }
            }

            return result;
        }

        private static IList<JobData> GetJobs(IScheduler scheduler, string groupName)
        {
            var result = new List<JobData>();

            foreach (var jobKey in scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEndsWith(groupName)))
            {
                result.Add(GetJobData(scheduler, jobKey));
            }

            return result;
        }

        private static JobData GetJobData(IScheduler scheduler,JobKey jobKey)
        {
            var jobData = new JobData(jobKey.Name, jobKey.Group, GetTriggers(scheduler, jobKey));
            jobData.Init();
            return jobData;
        }

        private static IList<TriggerData> GetTriggers(IScheduler scheduler, JobKey jobKey)
        {
            var result = new List<TriggerData>();

            foreach (var trigger in scheduler.GetTriggersOfJob(jobKey))
            {
                var data = new TriggerData(trigger.Key.Name, GetTriggerStatus(trigger, scheduler))
                {
                    StartDate = trigger.StartTimeUtc,
                    EndDate = trigger.EndTimeUtc,
                    NextFireDate = trigger.GetNextFireTimeUtc(),
                    PreviousFireDate = trigger.GetPreviousFireTimeUtc()
                };
                result.Add(data);
            }

            return result;
        }
    }
}