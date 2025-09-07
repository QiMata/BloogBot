using Microsoft.Extensions.Logging;
using Moq;
using WoWSharpClient.Agent;
using WoWSharpClient.Client;
using Xunit;

namespace WoWSharpClient.Tests.Agent
{
    public class AgentFactoryTests
    {
        private readonly Mock<IWorldClient> _mockWorldClient;
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<TargetingAgent>> _mockTargetingLogger;
        private readonly Mock<ILogger<AttackAgent>> _mockAttackLogger;

        public AgentFactoryTests()
        {
            _mockWorldClient = new Mock<IWorldClient>();
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockTargetingLogger = new Mock<ILogger<TargetingAgent>>();
            _mockAttackLogger = new Mock<ILogger<AttackAgent>>();

            // Setup the factory to return our specific loggers
            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(TargetingAgent).FullName!))
                .Returns(_mockTargetingLogger.Object);

            _mockLoggerFactory
                .Setup(x => x.CreateLogger(typeof(AttackAgent).FullName!))
                .Returns(_mockAttackLogger.Object);
        }

        [Fact]
        public void CreateTargetingAgent_WithLogger_ReturnsTargetingAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingAgent(_mockWorldClient.Object, _mockTargetingLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingAgent>(agent);
        }

        [Fact]
        public void CreateTargetingAgent_WithLoggerFactory_ReturnsTargetingAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingAgent>(agent);
            // Note: Cannot verify extension method calls with Moq
        }

        [Fact]
        public void CreateAttackAgent_WithLogger_ReturnsAttackAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackAgent(_mockWorldClient.Object, _mockAttackLogger.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackAgent>(agent);
        }

        [Fact]
        public void CreateAttackAgent_WithLoggerFactory_ReturnsAttackAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackAgent(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackAgent>(agent);
            // Note: Cannot verify extension method calls with Moq
        }

        [Fact]
        public void CreateTargetingAgentForClient_WithLoggerFactory_ReturnsTargetingAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingAgent>(agent);
            // Note: Cannot verify extension method calls with Moq
        }

        [Fact]
        public void CreateTargetingAgentForClient_WithoutLoggerFactory_ReturnsTargetingAgent()
        {
            // Act
            var agent = AgentFactory.CreateTargetingAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<TargetingAgent>(agent);
        }

        [Fact]
        public void CreateAttackAgentForClient_WithLoggerFactory_ReturnsAttackAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackAgentForClient(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackAgent>(agent);
            // Note: Cannot verify extension method calls with Moq
        }

        [Fact]
        public void CreateAttackAgentForClient_WithoutLoggerFactory_ReturnsAttackAgent()
        {
            // Act
            var agent = AgentFactory.CreateAttackAgentForClient(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(agent);
            Assert.IsType<AttackAgent>(agent);
        }

        [Fact]
        public void CreateCombatAgents_WithLoggerFactory_ReturnsBothAgents()
        {
            // Act
            var (targetingAgent, attackAgent) = AgentFactory.CreateCombatAgents(_mockWorldClient.Object, _mockLoggerFactory.Object);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.IsType<TargetingAgent>(targetingAgent);
            Assert.IsType<AttackAgent>(attackAgent);
            // Note: Cannot verify extension method calls with Moq
        }

        [Fact]
        public void CreateCombatAgents_WithoutLoggerFactory_ReturnsBothAgents()
        {
            // Act
            var (targetingAgent, attackAgent) = AgentFactory.CreateCombatAgents(_mockWorldClient.Object, null);

            // Assert
            Assert.NotNull(targetingAgent);
            Assert.NotNull(attackAgent);
            Assert.IsType<TargetingAgent>(targetingAgent);
            Assert.IsType<AttackAgent>(attackAgent);
        }

        [Fact]
        public void CreateTargetingAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateTargetingAgent(null!, _mockTargetingLogger.Object));
        }

        [Fact]
        public void CreateTargetingAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateTargetingAgent(_mockWorldClient.Object, (ILogger<TargetingAgent>)null!));
        }

        [Fact]
        public void CreateAttackAgent_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAttackAgent(null!, _mockAttackLogger.Object));
        }

        [Fact]
        public void CreateAttackAgent_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAttackAgent(_mockWorldClient.Object, (ILogger<AttackAgent>)null!));
        }

        [Fact]
        public void CreateTargetingAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateTargetingAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateAttackAgentForClient_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateAttackAgentForClient(null!, _mockLoggerFactory.Object));
        }

        [Fact]
        public void CreateCombatAgents_WithNullWorldClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                AgentFactory.CreateCombatAgents(null!, _mockLoggerFactory.Object));
        }
    }
}