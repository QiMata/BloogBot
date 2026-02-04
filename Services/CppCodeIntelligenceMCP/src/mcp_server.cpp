#include "../include/code_intelligence.h"
#include <nlohmann/json.hpp>
#include <iostream>
#include <fstream>
#include <sstream>
#include <thread>

using json = nlohmann::json;

namespace CodeIntelligence {

MCPServer::MCPServer(int port) : port(port), running(false) {
    symbolDb = std::make_unique<SymbolDatabase>();
    compileParser = std::make_unique<CompileCommandsParser>();
    astAnalyzer = std::make_unique<ASTAnalyzer>();
}

MCPServer::~MCPServer() {
    stop();
}

void MCPServer::start() {
    running = true;
    
    // Load compile commands if available
    if (compileParser->loadFromFile("compile_commands.json")) {
        std::cout << "Loaded compile commands database" << std::endl;
        
        // Analyze all files from compile commands
        for (const auto& command : compileParser->getCommands()) {
            auto symbols = astAnalyzer->analyzeFile(command.file, command);
            for (const auto& symbol : symbols) {
                symbolDb->addSymbol(symbol);
            }
        }
    }
    
    // Simple HTTP server simulation (in a real implementation, use proper HTTP library)
    std::cout << "C++ Code Intelligence MCP Server running on port " << port << std::endl;
    std::cout << "Available endpoints:" << std::endl;
    std::cout << "  GET /symbols?query=<name>" << std::endl;
    std::cout << "  GET /analyze?file=<path>" << std::endl;
    std::cout << "  GET /compile_commands" << std::endl;
    std::cout << "  GET /health" << std::endl;
    
    // Keep server running
    while (running) {
        std::this_thread::sleep_for(std::chrono::seconds(1));
    }
}

void MCPServer::stop() {
    running = false;
}

void MCPServer::handleRequest(const std::string& request) {
    // Basic request handling - in a real implementation, use proper HTTP parsing
    if (request.find("GET /symbols") != std::string::npos) {
        // Extract query parameter
        auto queryPos = request.find("query=");
        if (queryPos != std::string::npos) {
            auto query = request.substr(queryPos + 6);
            auto response = processSymbolQuery(query);
            std::cout << "Symbol query response: " << response << std::endl;
        }
    }
}

std::string MCPServer::processSymbolQuery(const std::string& query) {
    auto symbols = symbolDb->findSymbol(query);
    
    json response;
    response["query"] = query;
    response["symbols"] = json::array();
    
    for (const auto& symbol : symbols) {
        json symbolJson;
        symbolJson["name"] = symbol.name;
        symbolJson["type"] = symbol.type;
        symbolJson["file"] = symbol.file;
        symbolJson["line"] = symbol.line;
        symbolJson["column"] = symbol.column;
        symbolJson["signature"] = symbol.signature;
        symbolJson["scope"] = symbol.scope;
        response["symbols"].push_back(symbolJson);
    }
    
    return response.dump(2);
}

std::string MCPServer::processFileAnalysis(const std::string& filePath) {
    json response;
    response["file"] = filePath;
    response["analysis"] = "File analysis not implemented yet";
    return response.dump(2);
}

std::string MCPServer::processCompileCommands() {
    json response;
    response["commands"] = json::array();
    
    for (const auto& command : compileParser->getCommands()) {
        json cmdJson;
        cmdJson["directory"] = command.directory;
        cmdJson["command"] = command.command;
        cmdJson["file"] = command.file;
        response["commands"].push_back(cmdJson);
    }
    
    return response.dump(2);
}

} // namespace CodeIntelligence
