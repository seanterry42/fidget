﻿using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fidget.Commander.Dispatch
{
    /// <summary>
    /// Tests of the command adapter type.
    /// </summary>
    
    public class CommandAdapterTesting
    {
        /// <summary>
        /// Command type for testing.
        /// </summary>
        
        public class TestCommand : ICommand<object> {}

        /// <summary>
        /// Mock command handler.
        /// </summary>
        
        protected Mock<ICommandHandler<TestCommand,object>> MockHandler = new Mock<ICommandHandler<TestCommand, object>>();

        /// <summary>
        /// Decorators collection.
        /// </summary>
        
        protected List<ICommandDecorator<TestCommand,object>> Decorators = new List<ICommandDecorator<TestCommand, object>>();

        /// <summary>
        /// Creates and returns an instance using the configured arguments.
        /// </summary>
        
        protected ICommandAdapter<object> CreateInstance() => new CommandAdapter<TestCommand,object>( MockHandler?.Object, Decorators );
        
        /// <summary>
        /// Tests of the constructor method.
        /// </summary>
        
        public class Constructor : CommandAdapterTesting
        {
            [Fact]
            public void Requires_handler()
            {
                MockHandler = null;
                Assert.Throws<ArgumentNullException>( "handler", () => CreateInstance() );
            }

            [Fact]
            public void Requires_decorators()
            {
                Decorators = null;
                Assert.Throws<ArgumentNullException>( "decorators", () => CreateInstance() );
            }
        }

        /// <summary>
        /// Tests of the execute method.
        /// </summary>
        
        public class Execute : CommandAdapterTesting
        {
            /// <summary>
            /// Command argument.
            /// </summary>
            
            ICommand<object> Command = new TestCommand();

            /// <summary>
            /// Cancellation token argument.
            /// </summary>
            
            CancellationToken CancellationToken = CancellationToken.None;

            /// <summary>
            /// Calls the execute method with the configured values.
            /// </summary>
            
            Task<object> CallExecute() => CreateInstance().Execute( Command, CancellationToken );

            [Fact]
            public async Task Requires_command()
            {
                Command = null;
                await Assert.ThrowsAsync<ArgumentNullException>( "command", CallExecute );
            }

            /// <summary>
            /// Command type matching the expected interface.
            /// </summary>
            
            class WrongCommand : ICommand<object> {}

            [Fact]
            public async Task Throws_WhenCommandIsNotExpectedType()
            {
                Command = new WrongCommand();
                await Assert.ThrowsAsync<ArgumentException>( "command", CallExecute );
            }

            [Fact]
            public async Task Interrupts_WhenCancelled()
            {
                CancellationToken = new CancellationToken( true );
                await Assert.ThrowsAsync<OperationCanceledException>( CallExecute );
            }

            /// <summary>
            /// Tests with no decorators involved.
            /// </summary>
            
            [Fact]
            public async Task Returns_HandlerExecuteResult_WhenNoDecorators()
            {
                var expected = new object();
                var command = (TestCommand)Command;
                MockHandler.Setup( _=> _.Handle( command, CancellationToken ) ).ReturnsAsync( expected );
                
                var actual = await CallExecute();
                Assert.Equal( expected, actual );

                MockHandler.Verify( _=> _.Handle( command, CancellationToken ), Times.Once );
            }

            /// <summary>
            /// Test with decorators involved.
            /// </summary>
            
            [Fact]
            public async Task Returns_DecoratorExecuteResult_WhenDecorators()
            {
                var expected = new object();
                var command = (TestCommand)Command;
                var sequence = new MockSequence();
                var mockDecorators = new List<Mock<ICommandDecorator<TestCommand,object>>>();

                for ( var i = 0; i < 3; i++ )
                {
                    var decorator = new Mock<ICommandDecorator<TestCommand,object>>();
                    decorator
                        .InSequence( sequence )
                        .Setup( _=> _.Execute( command, CancellationToken, It.IsAny<CommandDelegate<TestCommand,object>>() ) )
                        .Returns( ( TestCommand cmd, CancellationToken token, CommandDelegate<TestCommand,object> continuation ) => continuation( cmd, token ) )
                        .Verifiable();

                    mockDecorators.Add( decorator );
                    Decorators.Add( decorator.Object );
                }

                MockHandler.InSequence( sequence ).Setup( _ => _.Handle( command, CancellationToken ) ).ReturnsAsync( expected );

                var actual = await CallExecute();
                Assert.Equal( expected, actual );

                foreach ( var decorator in mockDecorators )
                {
                    decorator.Verify( _=> _.Execute( command, CancellationToken, It.IsAny<CommandDelegate<TestCommand, object>>() ), Times.Once );
                }

                MockHandler.Verify( _=> _.Handle( command, CancellationToken ), Times.Once );
            }
        }
    }
}