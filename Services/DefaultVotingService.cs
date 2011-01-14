﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using Contrib.Voting.Events;
using Contrib.Voting.Functions;
using Contrib.Voting.Models;
using Orchard.Data;
using Orchard.Services;

namespace Contrib.Voting.Services {
    public class DefaultVotingService : IVotingService {
        private readonly IRepository<VoteRecord> _voteRepository;
        private readonly IRepository<ResultRecord> _resultRepository;
        private readonly IClock _clock;
        private readonly IFunctionCalculator _calculator;
        private readonly IEnumerable<IFunction> _functions;
        private readonly IVotingEventHandler _eventHandler;

        public DefaultVotingService(
            IRepository<VoteRecord> voteRepository,
            IRepository<ResultRecord> resultRepository,
            IClock clock,
            IFunctionCalculator calculator,
            IEnumerable<IFunction> functions,
            IVotingEventHandler eventHandler) {
            _voteRepository = voteRepository;
            _resultRepository = resultRepository;
            _clock = clock;
            _calculator = calculator;
            _functions = functions;
            _eventHandler = eventHandler;
        }

        public VoteRecord Get(int voteId) {
            return _voteRepository.Get(voteId);
        }

        public IEnumerable<VoteRecord> Get(Expression<Func<VoteRecord, bool>> predicate) {
            return _voteRepository.Fetch(predicate);
        }

        public void RemoveVote(VoteRecord vote) {

            foreach (var function in _functions) {
                _calculator.Calculate(new DeleteCalculus { Axe = vote.Axe, ContentId = vote.ContentItemRecord.Id, Vote = vote.Value, FunctionName = function.Name });
            }
            
            _voteRepository.Delete(vote);
            _eventHandler.VoteRemoved(vote);
        }

        public void RemoveVote(IEnumerable<VoteRecord> votes) {
            foreach(var vote in votes)
                _voteRepository.Delete(vote);
        }

        public void Vote(Orchard.ContentManagement.ContentItem contentItem, string userName, string hostname, double value, int axe = 0) {
            var vote = new VoteRecord {
                Axe = axe,
                ContentItemRecord = contentItem.Record,
                ContentType = contentItem.ContentType,
                CreatedUtc = _clock.UtcNow,
                Hostname = hostname,
                Username = userName,
                Value = value
            };

            _voteRepository.Create(vote);

            foreach(var function in _functions) {
                _calculator.Calculate(new CreateCalculus {Axe = axe, ContentId = contentItem.Id, FunctionName = function.Name, Vote = value});
            }

            _eventHandler.Voted(vote);
        }

        public void ChangeVote(VoteRecord vote, double value) {
            var previousValue = value;

            foreach (var function in _functions) {
                _calculator.Calculate(new UpdateCalculus { Axe = vote.Axe, ContentId = vote.ContentItemRecord.Id, PreviousVote = vote.Value, Vote = value, FunctionName = function.Name });
            }

            vote.CreatedUtc = _clock.UtcNow;
            vote.Value = value;

            _eventHandler.VoteChanged(vote, previousValue);
        }

        public IEnumerable<ResultRecord> GetResults(int contentItemId, int axe = 0, string[] functions = null) {

            foreach (var function in _functions) {
                var functionName = function.Name;
                yield return _resultRepository.Get(
                    r => r.Axe == axe
                         && r.ContentItemRecord.Id == contentItemId
                         && r.FunctionName == functionName);
            }
        }
    }
}