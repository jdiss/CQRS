﻿using System;
using System.Runtime.Serialization;
using Cqrs.Commands;
using Cqrs.Authentication;
using Cqrs.Messages;

namespace CQRSCode.WriteModel.Commands
{
	public class DeactivateInventoryItem : ICommand<ISingleSignOnToken>
	{
		public DeactivateInventoryItem(Guid id, int originalVersion)
		{
			Id = id;
			ExpectedVersion = originalVersion;
		}

		public Guid Id { get; set; }

		public int ExpectedVersion { get; set; }

		#region Implementation of IMessageWithAuthenticationToken<ISingleSignOnToken>

		public ISingleSignOnToken AuthenticationToken { get; set; }

		#endregion

		#region Implementation of IMessage

		public Guid CorrelationId { get; set; }

		[Obsolete("Use CorrelationId")]
		public Guid CorrolationId
		{
			get { return CorrelationId; }
			set { CorrelationId = value; }
		}

		[DataMember]
		public FrameworkType Framework { get; set; }

		#endregion
	}
}