using System;
using System.Collections.Generic;
using System.Linq;

namespace Antiban;

public class Antiban
{
    private readonly int serviceMessageType = 0;
    private readonly int infoMessageType = 1;

    private readonly TimeSpan infoMessageMinInterval = new(days: 1, hours: 0, minutes: 0, seconds: 0);
    private readonly TimeSpan serviceMessageMinInterval = new(hours: 0, minutes: 1, seconds: 0);
    private readonly TimeSpan anyMessageMinInterval = new(hours: 0, minutes: 0, seconds: 10);

    private readonly List<EventMessage> _messageStorage = new();

    /// <summary>
    /// Добавление сообщений в систему, для обработки порядка сообщений
    /// </summary>
    /// <param name="eventMessage"></param>
    public void PushEventMessage(EventMessage eventMessage)
    {
        _messageStorage.Add(eventMessage);
    }

    /// <summary>
    /// Вовзращает порядок отправок сообщений
    /// </summary>
    /// <returns></returns>
    public List<AntibanResult> GetResult()
    {
        List<EventMessage> messageStorageWithChanges = _messageStorage.Select(message =>
            new EventMessage(
                message.Id,
                message.Phone,
                message.DateTime,
                message.Priority)
            ).ToList();

        var intermediateResult = new List<AntibanResult>();

        var isAnyIntervalsChanged = true;
        var isInfoMessageIntervalChanged = false;
        var isServiceMessageIntervalChanged = false;
        var isGeneralMessageIntervalChanged = false;

        while (isAnyIntervalsChanged)
        {
            var groupedByPhone = messageStorageWithChanges.GroupBy(eventMessage => eventMessage.Phone);

            foreach (var eventMessagesByPhone in groupedByPhone)
            {
                var infoMessages = GetResultsListByPriority(eventMessagesByPhone, infoMessageType);
                isInfoMessageIntervalChanged = SetMinumumSendTimeInterval(infoMessages, infoMessageMinInterval);

                var serviceMessages = GetResultsListByPriority(eventMessagesByPhone, serviceMessageType);
                serviceMessages.AddRange(infoMessages);
                serviceMessages = serviceMessages
                    .OrderBy(message => message.SentDateTime)
                    .ToList();
                isServiceMessageIntervalChanged = SetMinumumSendTimeInterval(serviceMessages, serviceMessageMinInterval);

                intermediateResult.AddRange(serviceMessages);
            }

            intermediateResult = intermediateResult
                .OrderBy(message => message.SentDateTime)
                .ToList();

            isGeneralMessageIntervalChanged = SetMinumumSendTimeInterval(intermediateResult, anyMessageMinInterval);

            isAnyIntervalsChanged = isGeneralMessageIntervalChanged || isServiceMessageIntervalChanged || isInfoMessageIntervalChanged;
            if (isAnyIntervalsChanged)
            {
                foreach (var message in messageStorageWithChanges)
                {
                    message.DateTime = intermediateResult.First(r => r.EventMessageId == message.Id).SentDateTime;
                }
                intermediateResult.Clear();
            }
        }

        return intermediateResult;
    }

    /// <summary>
    /// Установка минимального интервала времени
    /// </summary>
    /// <param name="antibanResults">Входящий массив данных</param>
    /// <param name="minInterval">Минимальный интервал</param>
    /// <returns>Были ли внесены изменения</returns>
    private static bool SetMinumumSendTimeInterval(List<AntibanResult> antibanResults, TimeSpan minInterval)
    {
        var isChangesWasMade = false;

        if (antibanResults.Count > 1)
        {
            for (var index = 1; index < antibanResults.Count; index++)
            {
                var timeInterval = antibanResults[index].SentDateTime - antibanResults[index - 1].SentDateTime;
                if (timeInterval < minInterval)
                {
                    antibanResults[index].SentDateTime = antibanResults[index].SentDateTime.Add(minInterval - timeInterval);
                    isChangesWasMade = true;
                }
            }
        }

        return isChangesWasMade;
    }

    /// <summary>
    /// Получение данных по типу приоритета
    /// </summary>
    /// <param name="group">Входящий массив данных</param>
    /// <param name="targetPriority">Тип приоритета сообщания</param>
    /// <returns>Данные по заданному типу приоритета</returns>
    private static List<AntibanResult> GetResultsListByPriority(IGrouping<string, EventMessage> group, int targetPriority)
    {
        return group
                .Where(eventMessage => eventMessage.Priority == targetPriority)
                .OrderBy(eventMessage => eventMessage.DateTime)
                .Select(eventMessage => new AntibanResult
                {
                    EventMessageId = eventMessage.Id,
                    SentDateTime = eventMessage.DateTime
                })
                .ToList();
    }
}
